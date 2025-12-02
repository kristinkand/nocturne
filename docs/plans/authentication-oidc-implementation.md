# Nocturne Authentication & Authorization Implementation Plan

## Executive Summary

This document outlines a comprehensive plan for implementing OpenID Connect (OIDC) authentication in Nocturne while maintaining full backward compatibility with Nightscout's legacy v1, v2, and v3 API authentication systems. The solution uses opaque tokens with an internal token cache to enable immediate revocation without introspection latency.

---

## Table of Contents

1. [Goals & Requirements](#1-goals--requirements)
2. [Current State Analysis](#2-current-state-analysis)
3. [Architecture Overview](#3-architecture-overview)
4. [Token Strategy](#4-token-strategy)
5. [Database Schema Changes](#5-database-schema-changes)
6. [Implementation Phases](#6-implementation-phases)
7. [SSO & Healthcare Provider Integration](#7-sso--healthcare-provider-integration)
8. [Security Considerations](#8-security-considerations)
9. [Migration Strategy](#9-migration-strategy)
10. [Testing Strategy](#10-testing-strategy)

---

## 1. Goals & Requirements

### Primary Goals

| Goal | Description |
|------|-------------|
| **OIDC Authentication** | Modern authentication using OpenID Connect for the Nocturne web application |
| **Legacy API Compatibility** | Full backward compatibility with Nightscout v1/v2/v3 API authentication |
| **Opaque Tokens** | Use opaque tokens instead of JWTs for immediate revocability |
| **Token Caching** | Internal cache on resource server keyed by token ID with TTL matching token lifetime |
| **SSO Support** | Easy integration with hospital and healthcare provider identity systems |
| **Zero-Latency Revocation** | Avoid introspection endpoint latency via local cache invalidation |

### Authentication Methods to Support

| Method | API Version | Description |
|--------|-------------|-------------|
| `api-secret` header (SHA1 hash) | v1, v2, v3 | Legacy admin authentication via hashed API secret |
| Access Token (query/body/header) | v1, v2 | Nightscout subject access tokens |
| JWT Bearer Token | v2, v3 | Self-issued JWTs from access token exchange |
| OIDC Bearer Token | Modern | Opaque tokens from OIDC provider (new) |
| Session Cookie | Web | OIDC session for web application (new) |

---

## 2. Current State Analysis

### Existing Authentication Infrastructure

```
src/API/Nocturne.API/
├── Middleware/
│   └── AuthenticationMiddleware.cs    # Current auth middleware (JWT + API Secret)
├── Controllers/
│   ├── AuthenticationController.cs    # /api/v1/verifyauth endpoint
│   └── AuthorizationController.cs     # /api/v2/authorization/* endpoints
├── Services/
│   └── AuthorizationService.cs        # JWT generation, permission management
└── Extensions/
    └── HttpContextExtensions.cs       # Auth context helpers
```

### Current Authentication Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                    Current Authentication Flow                   │
└─────────────────────────────────────────────────────────────────┘

Request → AuthenticationMiddleware
           │
           ├─→ Bearer Token? ──→ Validate JWT ──→ Set AuthContext
           │
           ├─→ api-secret header? ──→ Validate SHA1 hash ──→ Set AuthContext (admin)
           │
           └─→ No credentials ──→ Set Unauthenticated
```

### Legacy Nightscout Authentication (from LegacyApp/lib/authorization/)

1. **API Secret**: SHA1 hash comparison grants admin (`*`) permissions
2. **Access Tokens**: Generated for Subjects, stored in MongoDB, format: `{abbrev}-{digest16chars}`
3. **JWT Exchange**: Access token → JWT via `/api/v2/authorization/request/:accessToken`
4. **Shiro-Trie Permissions**: Hierarchical permission system (e.g., `api:entries:read`)
5. **Roles**: Collections of permissions (admin, readable, careportal, etc.)
6. **Subjects**: Users/devices with assigned roles and access tokens

### Default Roles (from legacy storage.js)

| Role | Permissions |
|------|-------------|
| `admin` | `*` |
| `denied` | (none) |
| `status-only` | `api:status:read` |
| `readable` | `*:*:read` |
| `careportal` | `api:treatments:create` |
| `devicestatus-upload` | `api:devicestatus:create` |
| `activity` | `api:activity:create` |

---

## 3. Architecture Overview

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                         Nocturne Authentication Architecture                      │
└─────────────────────────────────────────────────────────────────────────────────┘

                                    ┌──────────────────┐
                                    │  OIDC Provider   │
                                    │  (Keycloak, etc) │
                                    └────────┬─────────┘
                                             │
              ┌──────────────────────────────┼──────────────────────────────┐
              │                              │                               │
              ▼                              ▼                               ▼
    ┌─────────────────┐           ┌─────────────────┐             ┌─────────────────┐
    │   Web Browser   │           │  Mobile Apps    │             │ CGM/Pump Devices│
    │  (Session/OIDC) │           │  (OIDC + Token) │             │  (Legacy Auth)  │
    └────────┬────────┘           └────────┬────────┘             └────────┬────────┘
             │                             │                                │
             │    ┌────────────────────────┼────────────────────────────────┤
             │    │                        │                                │
             ▼    ▼                        ▼                                ▼
    ┌─────────────────────────────────────────────────────────────────────────────┐
    │                            Nocturne API Gateway                              │
    │  ┌───────────────────────────────────────────────────────────────────────┐  │
    │  │                    Authentication Middleware Pipeline                  │  │
    │  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │  │
    │  │  │ OIDC Token  │→ │ Legacy JWT  │→ │ Access Token│→ │ API Secret  │  │  │
    │  │  │  Handler    │  │   Handler   │  │   Handler   │  │   Handler   │  │  │
    │  │  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘  │  │
    │  └───────────────────────────────────────────────────────────────────────┘  │
    │                                      │                                       │
    │  ┌───────────────────────────────────┼───────────────────────────────────┐  │
    │  │                        Token Cache Layer                               │  │
    │  │  ┌─────────────────────────────────────────────────────────────────┐  │  │
    │  │  │  In-Memory Cache (IMemoryCache)                                 │  │  │
    │  │  │  Key: token_id | Value: TokenCacheEntry | TTL: token_lifetime   │  │  │
    │  │  └─────────────────────────────────────────────────────────────────┘  │  │
    │  │  ┌─────────────────────────────────────────────────────────────────┐  │  │
    │  │  │  Distributed Cache (Redis - optional for multi-instance)        │  │  │
    │  │  │  Key: nocturne:token:{id} | Value: TokenCacheEntry | TTL: ...   │  │  │
    │  │  └─────────────────────────────────────────────────────────────────┘  │  │
    │  └───────────────────────────────────────────────────────────────────────┘  │
    └─────────────────────────────────────────────────────────────────────────────┘
                                             │
                                             ▼
    ┌─────────────────────────────────────────────────────────────────────────────┐
    │                              PostgreSQL Database                             │
    │  ┌────────────┐  ┌────────────┐  ┌────────────┐  ┌────────────────────────┐ │
    │  │  subjects  │  │   roles    │  │   tokens   │  │  oidc_user_mappings    │ │
    │  └────────────┘  └────────────┘  └────────────┘  └────────────────────────┘ │
    └─────────────────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility |
|-----------|----------------|
| **OIDC Provider** | External identity provider (Keycloak, Auth0, Azure AD, hospital SSO) |
| **Authentication Middleware** | Multi-scheme auth handling with fallback chain |
| **Token Cache** | Local cache for opaque tokens to avoid introspection latency |
| **Token Store** | Persistent storage for issued tokens, revocation tracking |
| **Subject/Role Store** | Permission and role management (legacy compatibility) |

---

## 4. Token Strategy

### Opaque Token Design

We'll use opaque tokens (random strings) instead of JWTs for external issuance. This provides:

1. **Immediate Revocability**: Token can be invalidated in cache without waiting for expiry
2. **No Information Leakage**: Token content is meaningless without server lookup
3. **Smaller Size**: Opaque tokens are typically shorter than JWTs
4. **Server-Side Control**: All validation happens server-side

### Token Format

```
Opaque Token Format:
┌──────────────────────────────────────────────────────────────┐
│  nocturne_{version}_{token_id}_{random_bytes}                │
│                                                              │
│  Example: nocturne_v1_01JDQXYZ..._{32_char_random}           │
│           ────────── ── ────────── ───────────────           │
│           prefix    ver  ULID       cryptographic nonce      │
└──────────────────────────────────────────────────────────────┘

Legacy Access Token Format (maintained for compatibility):
┌──────────────────────────────────────────────────────────────┐
│  {name_abbrev}-{16_char_sha1_digest}                         │
│                                                              │
│  Example: rhys-a1b2c3d4e5f6g7h8                              │
└──────────────────────────────────────────────────────────────┘
```

### Token Cache Strategy

```csharp
// Token Cache Entry Structure
public class TokenCacheEntry
{
    public string TokenId { get; set; }           // ULID/GUID
    public string SubjectId { get; set; }         // User/device ID
    public string? OidcSubjectId { get; set; }    // External OIDC 'sub' claim
    public string? OidcIssuer { get; set; }       // OIDC issuer URL
    public List<string> Permissions { get; set; } // Resolved permissions
    public List<string> Roles { get; set; }       // Assigned roles
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public TokenType Type { get; set; }           // OIDC, Legacy, ApiSecret
}

public enum TokenType
{
    OidcOpaque,      // New OIDC-issued opaque token
    LegacyAccess,    // Nightscout-style access token
    LegacyJwt,       // Nightscout-style self-issued JWT
    ApiSecret        // API_SECRET hash authentication
}
```

### Cache TTL Strategy

| Token Type | Cache TTL | Rationale |
|------------|-----------|-----------|
| OIDC Opaque | Token lifetime (configurable, default 1 hour) | Match token expiry |
| Legacy Access | Session duration or 24 hours | Long-lived device tokens |
| Legacy JWT | JWT expiry claim | Match self-issued JWT |
| API Secret | 5 minutes | Frequently validated, short cache |

### Cache Invalidation

```
Token Revocation Flow:
┌─────────────────────────────────────────────────────────────┐
│ 1. Admin/User requests token revocation                     │
│ 2. Mark token as revoked in database                        │
│ 3. Broadcast cache invalidation event (if distributed)      │
│ 4. Remove from local cache immediately                       │
│ 5. Next request with token fails auth                       │
└─────────────────────────────────────────────────────────────┘
```

---

## 5. Database Schema Changes

### New Entities

```csharp
// src/Infrastructure/Nocturne.Infrastructure.Data/Entities/TokenEntity.cs
/// <summary>
/// OIDC-issued tokens and legacy access tokens
/// </summary>
[Table("tokens")]
public class TokenEntity
{
    public Guid Id { get; set; }
    
    /// <summary>SHA256 hash of the opaque token</summary>
    [MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;
    
    /// <summary>Token type: 'oidc_opaque', 'legacy_access', 'legacy_jwt', 'api_secret'</summary>
    [MaxLength(20)]
    public string TokenType { get; set; } = string.Empty;
    
    public Guid? SubjectId { get; set; }
    public SubjectEntity? Subject { get; set; }
    
    /// <summary>External OIDC 'sub' claim</summary>
    [MaxLength(255)]
    public string? OidcSubjectId { get; set; }
    
    /// <summary>OIDC issuer URL</summary>
    [MaxLength(500)]
    public string? OidcIssuer { get; set; }
    
    /// <summary>For OIDC session logout</summary>
    [MaxLength(255)]
    public string? OidcSessionId { get; set; }
    
    /// <summary>OAuth2 scopes (stored as JSON array)</summary>
    public List<string> Scopes { get; set; } = new();
    
    /// <summary>Resolved Shiro-style permissions</summary>
    public List<string> Permissions { get; set; } = new();
    
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    
    [MaxLength(255)]
    public string? RevokedReason { get; set; }
    
    /// <summary>OAuth2 client ID</summary>
    [MaxLength(255)]
    public string? ClientId { get; set; }
    
    /// <summary>Device info JSON (user agent, IP, etc.)</summary>
    public string? DeviceInfoJson { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// src/Infrastructure/Nocturne.Infrastructure.Data/Entities/SubjectEntity.cs
/// <summary>
/// Subjects (users/devices) - enhanced from legacy Nightscout
/// </summary>
[Table("subjects")]
public class SubjectEntity
{
    public Guid Id { get; set; }
    
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>SHA256 hash of legacy access token</summary>
    [MaxLength(64)]
    public string? AccessTokenHash { get; set; }
    
    /// <summary>Display prefix for access token: "rhys-a1b2..."</summary>
    [MaxLength(50)]
    public string? AccessTokenPrefix { get; set; }
    
    /// <summary>Link to OIDC identity</summary>
    [MaxLength(255)]
    public string? OidcSubjectId { get; set; }
    
    [MaxLength(500)]
    public string? OidcIssuer { get; set; }
    
    [MaxLength(255)]
    public string? Email { get; set; }
    
    public string? Notes { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    
    // Navigation properties
    public ICollection<SubjectRoleEntity> SubjectRoles { get; set; } = new List<SubjectRoleEntity>();
    public ICollection<TokenEntity> Tokens { get; set; } = new List<TokenEntity>();
}

// src/Infrastructure/Nocturne.Infrastructure.Data/Entities/RoleEntity.cs
/// <summary>
/// Roles - enhanced from legacy Nightscout
/// </summary>
[Table("roles")]
public class RoleEntity
{
    public Guid Id { get; set; }
    
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Shiro-style permissions for this role</summary>
    public List<string> Permissions { get; set; } = new();
    
    public string? Notes { get; set; }
    
    /// <summary>Protect system-generated default roles from deletion</summary>
    public bool IsSystemRole { get; set; } = false;
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation property
    public ICollection<SubjectRoleEntity> SubjectRoles { get; set; } = new List<SubjectRoleEntity>();
}

// src/Infrastructure/Nocturne.Infrastructure.Data/Entities/SubjectRoleEntity.cs
/// <summary>
/// Subject-Role mapping (many-to-many)
/// </summary>
[Table("subject_roles")]
public class SubjectRoleEntity
{
    public Guid SubjectId { get; set; }
    public SubjectEntity? Subject { get; set; }
    
    public Guid RoleId { get; set; }
    public RoleEntity? Role { get; set; }
    
    public DateTime AssignedAt { get; set; }
    
    /// <summary>Who assigned this role (null for system-assigned)</summary>
    public Guid? AssignedById { get; set; }
    public SubjectEntity? AssignedBy { get; set; }
}

// src/Infrastructure/Nocturne.Infrastructure.Data/Entities/OidcProviderEntity.cs
/// <summary>
/// OIDC Provider Configuration (for multi-provider SSO)
/// </summary>
[Table("oidc_providers")]
public class OidcProviderEntity
{
    public Guid Id { get; set; }
    
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required, MaxLength(500)]
    public string IssuerUrl { get; set; } = string.Empty;
    
    [Required, MaxLength(255)]
    public string ClientId { get; set; } = string.Empty;
    
    /// <summary>Encrypted client secret</summary>
    public byte[]? ClientSecretEncrypted { get; set; }
    
    /// <summary>Cached OIDC discovery document (JSON)</summary>
    public string? DiscoveryDocumentJson { get; set; }
    
    public DateTime? DiscoveryCachedAt { get; set; }
    
    /// <summary>OAuth2 scopes to request</summary>
    public List<string> Scopes { get; set; } = new() { "openid", "profile", "email" };
    
    /// <summary>Claim mappings JSON (map OIDC claims to Nocturne)</summary>
    public string ClaimMappingsJson { get; set; } = "{}";
    
    /// <summary>Default roles to assign to users from this provider</summary>
    public List<string> DefaultRoles { get; set; } = new() { "readable" };
    
    public bool IsEnabled { get; set; } = true;
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// src/Infrastructure/Nocturne.Infrastructure.Data/Entities/AuthAuditLogEntity.cs
/// <summary>
/// Audit log for security events
/// </summary>
[Table("auth_audit_log")]
public class AuthAuditLogEntity
{
    public Guid Id { get; set; }
    
    /// <summary>Event type: 'login', 'logout', 'token_issued', 'revoked', 'failed_auth'</summary>
    [Required, MaxLength(50)]
    public string EventType { get; set; } = string.Empty;
    
    public Guid? SubjectId { get; set; }
    public SubjectEntity? Subject { get; set; }
    
    public Guid? TokenId { get; set; }
    public TokenEntity? Token { get; set; }
    
    [MaxLength(45)] // IPv6 max length
    public string? IpAddress { get; set; }
    
    public string? UserAgent { get; set; }
    
    /// <summary>Additional event details (JSON)</summary>
    public string? DetailsJson { get; set; }
    
    public DateTime CreatedAt { get; set; }
}
```

### DbContext Configuration

```csharp
// In NocturneDbContext.OnModelCreating()

// Token entity configuration
modelBuilder.Entity<TokenEntity>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasValueGenerator<GuidV7ValueGenerator>();
    
    entity.HasIndex(e => e.TokenHash).IsUnique();
    entity.HasIndex(e => e.SubjectId);
    entity.HasIndex(e => e.OidcSubjectId);
    entity.HasIndex(e => e.ExpiresAt);
    entity.HasIndex(e => e.RevokedAt).HasFilter("revoked_at IS NULL");
    
    entity.Property(e => e.Scopes).HasColumnType("jsonb");
    entity.Property(e => e.Permissions).HasColumnType("jsonb");
    
    entity.HasOne(e => e.Subject)
          .WithMany(s => s.Tokens)
          .HasForeignKey(e => e.SubjectId)
          .OnDelete(DeleteBehavior.SetNull);
});

// Subject entity configuration
modelBuilder.Entity<SubjectEntity>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasValueGenerator<GuidV7ValueGenerator>();
    
    entity.HasIndex(e => e.Name);
    entity.HasIndex(e => e.AccessTokenHash).IsUnique();
    entity.HasIndex(e => new { e.OidcSubjectId, e.OidcIssuer }).IsUnique();
});

// Role entity configuration
modelBuilder.Entity<RoleEntity>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasValueGenerator<GuidV7ValueGenerator>();
    
    entity.HasIndex(e => e.Name).IsUnique();
    entity.Property(e => e.Permissions).HasColumnType("jsonb");
});

// SubjectRole (many-to-many) configuration
modelBuilder.Entity<SubjectRoleEntity>(entity =>
{
    entity.HasKey(e => new { e.SubjectId, e.RoleId });
    
    entity.HasOne(e => e.Subject)
          .WithMany(s => s.SubjectRoles)
          .HasForeignKey(e => e.SubjectId)
          .OnDelete(DeleteBehavior.Cascade);
    
    entity.HasOne(e => e.Role)
          .WithMany(r => r.SubjectRoles)
          .HasForeignKey(e => e.RoleId)
          .OnDelete(DeleteBehavior.Cascade);
    
    entity.HasOne(e => e.AssignedBy)
          .WithMany()
          .HasForeignKey(e => e.AssignedById)
          .OnDelete(DeleteBehavior.SetNull);
});

// OIDC Provider configuration
modelBuilder.Entity<OidcProviderEntity>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasValueGenerator<GuidV7ValueGenerator>();
    
    entity.HasIndex(e => e.IssuerUrl).IsUnique();
    entity.Property(e => e.Scopes).HasColumnType("jsonb");
    entity.Property(e => e.DefaultRoles).HasColumnType("jsonb");
});

// Auth Audit Log configuration
modelBuilder.Entity<AuthAuditLogEntity>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasValueGenerator<GuidV7ValueGenerator>();
    
    entity.HasIndex(e => e.SubjectId);
    entity.HasIndex(e => e.EventType);
    entity.HasIndex(e => e.CreatedAt).IsDescending();
    
    entity.HasOne(e => e.Subject)
          .WithMany()
          .HasForeignKey(e => e.SubjectId)
          .OnDelete(DeleteBehavior.SetNull);
    
    entity.HasOne(e => e.Token)
          .WithMany()
          .HasForeignKey(e => e.TokenId)
          .OnDelete(DeleteBehavior.SetNull);
});
```

---

## 6. Implementation Phases

### Phase 1: Foundation (Week 1-2)

**Objective**: Set up database schema, core token service, and caching infrastructure.

#### Tasks

1. **Database Migration**
   - Create new entity classes: `TokenEntity`, `SubjectEntity`, `RoleEntity`, `SubjectRoleEntity`, `OidcProviderEntity`, `AuthAuditLogEntity`
   - Add EF Core configurations and migrations
   - Seed default roles (admin, readable, careportal, etc.)

2. **Token Cache Service**
   ```csharp
   // src/Infrastructure/Nocturne.Infrastructure.Cache/Services/TokenCacheService.cs
   public interface ITokenCacheService
   {
       Task<TokenCacheEntry?> GetAsync(string tokenHash);
       Task SetAsync(string tokenHash, TokenCacheEntry entry);
       Task InvalidateAsync(string tokenHash);
       Task InvalidateBySubjectAsync(Guid subjectId);
       Task<bool> IsRevokedAsync(string tokenHash);
   }
   ```

3. **Token Store Service**
   ```csharp
   // src/Core/Nocturne.Core.Contracts/ITokenService.cs
   public interface ITokenService
   {
       Task<Token> CreateTokenAsync(CreateTokenRequest request);
       Task<Token?> ValidateTokenAsync(string opaqueToken);
       Task<bool> RevokeTokenAsync(Guid tokenId, string reason);
       Task<bool> RevokeAllTokensForSubjectAsync(Guid subjectId, string reason);
       Task<List<Token>> GetActiveTokensForSubjectAsync(Guid subjectId);
       Task PruneExpiredTokensAsync();
   }
   ```

4. **Subject/Role Repository**
   ```csharp
   // src/Infrastructure/Nocturne.Infrastructure.Data/Repositories/SubjectRepository.cs
   public interface ISubjectRepository
   {
       Task<SubjectEntity?> GetByAccessTokenAsync(string accessToken);
       Task<SubjectEntity?> GetByOidcIdentityAsync(string oidcSubjectId, string issuer);
       Task<SubjectEntity> CreateOrUpdateFromOidcAsync(OidcUserInfo userInfo);
       Task<List<string>> GetPermissionsAsync(Guid subjectId);
   }
   ```

#### Deliverables

- [ ] Database migrations for auth tables
- [ ] `ITokenCacheService` with `IMemoryCache` implementation
- [ ] `ITokenService` implementation
- [ ] `ISubjectRepository` and `IRoleRepository` implementations
- [ ] Unit tests for all new services

---

### Phase 2: Legacy Compatibility Layer (Week 2-3)

**Objective**: Refactor existing auth middleware to use new services while maintaining legacy API compatibility.

#### Tasks

1. **Refactor AuthenticationMiddleware**
   ```csharp
   public class AuthenticationMiddleware
   {
       // Handler chain: OIDC → Legacy JWT → Access Token → API Secret
       private readonly IAuthHandler[] _handlers;
       
       public async Task InvokeAsync(HttpContext context)
       {
           foreach (var handler in _handlers)
           {
               var result = await handler.AuthenticateAsync(context);
               if (result.Succeeded)
               {
                   context.Items["AuthContext"] = result.AuthContext;
                   break;
               }
           }
           await _next(context);
       }
   }
   ```

2. **Create Auth Handlers**
   ```csharp
   public interface IAuthHandler
   {
       int Priority { get; }
       Task<AuthResult> AuthenticateAsync(HttpContext context);
   }
   
   // Implementations:
   // - OidcTokenHandler (new)
   // - LegacyJwtHandler (refactored)
   // - AccessTokenHandler (refactored)
   // - ApiSecretHandler (refactored)
   ```

3. **Update AuthorizationController for Token Exchange**
   ```csharp
   // Maintain: GET /api/v2/authorization/request/:accessToken
   // Returns JWT for legacy clients
   
   // Add: POST /api/v2/authorization/token
   // Modern token endpoint (returns opaque token)
   ```

4. **Migrate API Endpoints**
   - `/api/v1/verifyauth` - Works with all auth methods
   - `/api/v2/authorization/*` - Subject/role management
   - `/api/v3/*` - Bearer token only (OIDC or legacy JWT)

#### Deliverables

- [ ] Handler-based authentication pipeline
- [ ] `OidcTokenHandler`, `LegacyJwtHandler`, `AccessTokenHandler`, `ApiSecretHandler`
- [ ] Updated authorization endpoints
- [ ] Integration tests for legacy API compatibility

---

### Phase 3: OIDC Integration (Week 3-4)

**Objective**: Implement full OIDC authentication flow for the web application.

#### Tasks

1. **OIDC Configuration Service**
   ```csharp
   public interface IOidcProviderService
   {
       Task<OidcProvider?> GetProviderByIssuerAsync(string issuer);
       Task<OidcDiscoveryDocument> GetDiscoveryDocumentAsync(string issuer);
       Task<List<OidcProvider>> GetEnabledProvidersAsync();
       Task<OidcProvider> ConfigureProviderAsync(OidcProviderConfig config);
   }
   ```

2. **Authentication Flow Endpoints**
   ```csharp
   // src/API/Nocturne.API/Controllers/OidcController.cs
   
   [Route("auth")]
   public class OidcController : ControllerBase
   {
       // GET /auth/login?provider={providerId}&returnUrl={url}
       // Initiates OIDC authorization code flow
       
       // GET /auth/callback
       // Handles OIDC callback, exchanges code for tokens
       
       // POST /auth/logout
       // Revokes tokens, performs OIDC logout if supported
       
       // GET /auth/userinfo
       // Returns current user info from session
       
       // POST /auth/refresh
       // Refresh token flow
   }
   ```

3. **Token Exchange for Opaque Tokens**
   ```csharp
   // After OIDC callback:
   // 1. Validate ID token from OIDC provider
   // 2. Create/update Subject from OIDC claims
   // 3. Generate opaque access token
   // 4. Store token in database with hash
   // 5. Cache token entry
   // 6. Return opaque token to client (or set session cookie)
   ```

4. **Session Management for Web**
   ```csharp
   // Cookie-based sessions for web app
   // SameSite=Strict, HttpOnly, Secure
   // Contains encrypted reference to opaque token
   ```

#### Deliverables

- [ ] OIDC provider configuration system
- [ ] Authorization code flow implementation
- [ ] Session cookie management
- [ ] Token refresh flow
- [ ] Logout (local + OIDC RP-initiated)
- [ ] E2E tests for OIDC flows

---

### Phase 4: Web Frontend Integration (Week 4-5)

**Objective**: Update SvelteKit frontend to use OIDC authentication.

#### Tasks

1. **Auth Store (Svelte)**
   ```typescript
   // src/Web/packages/app/src/lib/stores/auth.ts
   interface AuthState {
     isAuthenticated: boolean;
     user: UserInfo | null;
     permissions: string[];
     loading: boolean;
   }
   
   export const auth = writable<AuthState>({...});
   
   export async function login(providerId?: string): Promise<void>;
   export async function logout(): Promise<void>;
   export async function refreshToken(): Promise<void>;
   export function hasPermission(permission: string): boolean;
   ```

2. **Auth Hooks (SvelteKit)**
   ```typescript
   // src/Web/packages/app/src/hooks.server.ts
   export const handle: Handle = async ({ event, resolve }) => {
     // Extract session from cookie
     // Validate token via API
     // Populate event.locals.user
   };
   ```

3. **Protected Routes**
   ```typescript
   // src/Web/packages/app/src/routes/(protected)/+layout.server.ts
   export const load: LayoutServerLoad = async ({ locals, url }) => {
     if (!locals.user) {
       throw redirect(303, `/auth/login?returnUrl=${url.pathname}`);
     }
     return { user: locals.user };
   };
   ```

4. **Login UI Components**
   - Provider selection (for multi-SSO)
   - Login form (if local auth enabled)
   - User dropdown with logout
   - Session expiry warning

#### Deliverables

- [ ] Svelte auth store and hooks
- [ ] Login/logout pages
- [ ] Protected route layouts
- [ ] User profile components
- [ ] Session expiry handling

---

### Phase 5: OIDC Provider Configuration (Week 5-6)

**Objective**: Allow users to configure their own OIDC providers for SSO.

#### Tasks

1. **OIDC Provider Management API**
   ```csharp
   // src/API/Nocturne.API/Controllers/OidcProviderController.cs
   [Route("api/admin/oidc-providers")]
   public class OidcProviderController : ControllerBase
   {
       // GET  - List configured providers
       // POST - Add new provider
       // PUT  - Update provider
       // DELETE - Remove provider
       // POST /test - Test provider connectivity
   }
   ```

2. **Provider Configuration via appsettings.json**
   ```json
   {
     "Oidc": {
       "Providers": [
         {
           "Name": "Keycloak",
           "IssuerUrl": "https://auth.example.com/realms/nocturne",
           "ClientId": "nocturne-web",
           "ClientSecret": "...",
           "Scopes": ["openid", "profile", "email"],
           "DefaultRoles": ["readable"]
         }
       ]
     }
   }
   ```

3. **Documentation for Common Providers**
   - Keycloak setup guide
   - Azure AD / Entra ID setup guide
   - Okta setup guide
   - Generic OIDC provider guide

#### Deliverables

- [ ] OIDC provider CRUD API endpoints
- [ ] Provider connectivity test endpoint
- [ ] Configuration via appsettings.json
- [ ] Setup documentation for common providers

> **📌 POST-ALPHA**: Nocturne-hosted SSO with domain verification, HCP-specific roles, and patient sharing features will be added after alpha.

---

### Phase 6: Distributed Cache & Production Hardening (Week 6-7)

**Objective**: Add Redis distributed caching for multi-instance deployments and security hardening.

#### Tasks

1. **Redis Token Cache**
   ```csharp
   public class DistributedTokenCacheService : ITokenCacheService
   {
       private readonly IDistributedCache _cache;
       private readonly IMemoryCache _localCache;  // L1 cache
       
       // Pattern: Check local → Check Redis → Database
       // Invalidation: Pub/sub broadcast to all instances
   }
   ```

2. **Cache Invalidation Pub/Sub**
   ```csharp
   // When token is revoked:
   // 1. Update database
   // 2. Remove from local cache
   // 3. Publish invalidation event to Redis channel
   // 4. All subscribers remove from their local cache
   ```

3. **Rate Limiting**
   ```csharp
   // Per-IP rate limits for auth endpoints
   // Sliding window: 5 failed attempts → 1 min lockout
   // Exponential backoff for repeated failures
   ```

4. **Security Audit Logging**
   - Log all auth events to `auth_audit_log`
   - Configurable retention policy
   - Export to SIEM systems

5. **Token Rotation**
   - Automatic token rotation before expiry
   - Refresh token sliding window
   - Maximum token lifetime enforcement

#### Deliverables

- [ ] Redis cache integration
- [ ] Pub/sub cache invalidation
- [ ] Rate limiting middleware
- [ ] Audit logging
- [ ] Token rotation service
- [ ] Security documentation

---

## 7. SSO & Healthcare Provider Integration

> **📌 POST-ALPHA**: This section describes the HCP SSO system planned for after the alpha release. For alpha, we will support standard OIDC providers (Keycloak, Azure AD, etc.) that users configure themselves. The Nocturne-hosted SSO with domain verification will be implemented in a future release.

### Nocturne-Hosted SSO (Future - Post-Alpha)

Rather than requiring hospitals to configure their own OIDC infrastructure, **Nocturne will host a centralized identity service** that healthcare providers can use with minimal setup. This "plug-and-play" approach requires only domain verification.

#### How It Works

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                    Nocturne Hosted SSO Architecture                              │
└─────────────────────────────────────────────────────────────────────────────────┘

  Hospital Staff                    Nocturne Cloud                    GitHub Repo
  ─────────────                     ──────────────                    ───────────
       │                                  │                                │
       │  1. Visit nocturne.app           │                                │
       │─────────────────────────────────▶│                                │
       │                                  │                                │
       │  2. Click "Login with Hospital"  │                                │
       │─────────────────────────────────▶│                                │
       │                                  │                                │
       │  3. Enter work email             │                                │
       │     (nurse@mercy.hospital.org)   │                                │
       │─────────────────────────────────▶│                                │
       │                                  │                                │
       │                                  │  4. Check verified domains     │
       │                                  │─────────────────────────────────▶
       │                                  │                                │
       │                                  │  5. Found: mercy.hospital.org  │
       │                                  │◀─────────────────────────────────
       │                                  │                                │
       │  6. Send magic link/OTP email    │                                │
       │◀─────────────────────────────────│                                │
       │                                  │                                │
       │  7. Click link / Enter OTP       │                                │
       │─────────────────────────────────▶│                                │
       │                                  │                                │
       │  8. Authenticated! Assign roles  │                                │
       │◀─────────────────────────────────│                                │
       │     based on domain config       │                                │
       └                                  └                                └
```

#### Domain Verification Repository

A public GitHub repository (`nightscout/nocturne-verified-domains`) will store verified healthcare provider domains:

```
nightscout/nocturne-verified-domains/
├── README.md                           # How to request verification
├── schema/
│   └── domain.schema.json              # JSON schema for domain configs
├── domains/
│   ├── us/
│   │   ├── mercy-hospital-org.json
│   │   ├── mayo-clinic-org.json
│   │   └── kaiser-permanente-org.json
│   ├── uk/
│   │   ├── nhs-uk.json
│   │   └── gosh-nhs-uk.json
│   ├── eu/
│   │   ├── charite-de.json
│   │   └── aphp-fr.json
│   └── au/
│       └── health-nsw-gov-au.json
└── pending/                            # PRs for new domains go here first
```

#### Domain Configuration Schema

```json
// domains/us/mercy-hospital-org.json
{
  "$schema": "../schema/domain.schema.json",
  "domain": "mercy.hospital.org",
  "organization": {
    "name": "Mercy Hospital",
    "type": "hospital",
    "country": "US",
    "region": "Missouri",
    "npi": "1234567890",           // National Provider Identifier (US)
    "verified_at": "2025-01-15",
    "verified_by": "rhysg"
  },
  "authentication": {
    "method": "email_otp",          // or "magic_link", "passkey"
    "mfa_required": false,          // Can require MFA for this org
    "session_timeout_hours": 8,
    "allowed_email_patterns": [
      "*@mercy.hospital.org",
      "*@mercyhealth.org"           // Multiple domains for same org
    ]
  },
  "authorization": {
    "default_roles": ["hcp-viewer"],
    "role_mappings": {
      // Map email patterns to specific roles
      "admin-*@mercy.hospital.org": ["hcp-admin"],
      "diabetes-*@mercy.hospital.org": ["hcp-full-access"],
      "*@mercy.hospital.org": ["hcp-viewer"]
    },
    "max_patients_per_hcp": 100,    // Rate limiting
    "data_retention_days": 90       // How long HCP can view patient data
  },
  "contact": {
    "admin_email": "it-security@mercy.hospital.org",
    "admin_name": "IT Security Team"
  }
}
```

#### Verification Process

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                      Domain Verification Workflow                                │
└─────────────────────────────────────────────────────────────────────────────────┘

1. HOSPITAL IT SUBMITS REQUEST
   ├── Opens GitHub Issue using template
   ├── Provides: domain, org name, admin contact, proof of employment
   └── Signs BAA (Business Associate Agreement) if in US

2. NOCTURNE TEAM VERIFIES
   ├── Check domain ownership (DNS TXT record or email verification)
   ├── Verify organization legitimacy (NPI lookup, website check)
   ├── Review submitted documentation
   └── Confirm admin contact works at organization

3. VERIFICATION METHODS (choose one)
   ├── DNS TXT Record: Add "nocturne-verify=abc123" to domain DNS
   ├── Admin Email: Send verification to admin@[domain]
   ├── WHOIS Match: Organization matches domain WHOIS
   └── Manual: Video call with hospital IT (for complex cases)

4. APPROVAL
   ├── Maintainer approves PR moving config from pending/ to domains/
   ├── Automated CI validates JSON schema
   ├── Config is live within minutes (GitHub webhook or periodic sync)
   └── Hospital IT admin receives confirmation email

5. ONGOING
   ├── Annual re-verification reminder
   ├── Hospital can submit PR to update config
   └── Emergency revocation via PR or direct maintainer action
```

#### GitHub Issue Template for Domain Requests

```markdown
<!-- .github/ISSUE_TEMPLATE/verify-domain.md -->
---
name: Healthcare Domain Verification Request
about: Request verification of a healthcare provider domain for Nocturne SSO
title: "[DOMAIN] example.hospital.org"
labels: domain-verification, pending
---

## Organization Information

**Organization Name:** 
**Domain(s) to Verify:** 
**Country:** 
**Organization Type:** (Hospital / Clinic / Health System / Research Institution)

## Verification Information

**Your Name:** 
**Your Role:** 
**Your Work Email:** 
**Admin Contact Email:** (for domain ownership verification)

## Proof of Organization

Please provide ONE of the following:
- [ ] NPI Number (US): 
- [ ] NHS ODS Code (UK): 
- [ ] Link to organization's official website: 
- [ ] Other healthcare registration ID: 

## DNS Verification (Preferred)

Add this TXT record to your domain's DNS:
```
TXT nocturne-verify=[GENERATED_TOKEN]
```

- [ ] I have added the DNS TXT record

## Agreements

- [ ] I confirm I am authorized to request domain verification for this organization
- [ ] I understand that this enables staff with @domain emails to access patient data
- [ ] I agree to notify Nocturne if domain ownership changes
- [ ] (US Only) I confirm our organization will sign a BAA if required

## Additional Context

(Any additional information about your organization or special requirements)
```

### Authentication Methods for HCPs

#### Email OTP (Default)

The simplest approach - staff enter their work email, receive a 6-digit code:

```typescript
// Flow implementation
interface EmailOtpFlow {
  // 1. User enters email
  requestOtp(email: string): Promise<{ sent: boolean; expiresIn: number }>;
  
  // 2. Validate email domain against verified domains
  // 3. Send OTP via email (valid for 10 minutes)
  // 4. User enters OTP
  
  verifyOtp(email: string, otp: string): Promise<AuthResult>;
  
  // 5. Create session, assign roles based on domain config
}
```

#### Magic Link (Alternative)

One-click login via email link:

```typescript
interface MagicLinkFlow {
  // 1. User enters email
  requestMagicLink(email: string, returnUrl: string): Promise<void>;
  
  // 2. Email contains: https://auth.nocturne.app/verify?token=xxx
  // 3. Token is single-use, expires in 15 minutes
  
  verifyMagicLink(token: string): Promise<AuthResult>;
}
```

#### Passkey (Future Enhancement)

For hospitals that want stronger security without passwords:

```typescript
interface PasskeyFlow {
  // WebAuthn registration after initial email verification
  registerPasskey(email: string): Promise<PublicKeyCredential>;
  
  // Future logins use device biometrics
  authenticateWithPasskey(): Promise<AuthResult>;
}
```

### HCP Role Hierarchy

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                         Healthcare Provider Roles                                │
└─────────────────────────────────────────────────────────────────────────────────┘

Role                    Permissions                              Use Case
────                    ───────────                              ────────

hcp-viewer              - View patient glucose data (read-only)  General hospital staff
                        - View patient profiles                  who may need quick access
                        - No treatment data access
                        
hcp-full-access         - All viewer permissions                 Diabetes care team,
                        - View treatments                        endocrinologists,
                        - View device status                     certified diabetes
                        - Export reports                         educators

hcp-admin               - All full-access permissions            Hospital IT admin,
                        - Invite other HCPs from domain          department head
                        - Manage patient-HCP relationships
                        - View audit logs for their org

nocturne-admin          - Full system access                     Nocturne team only
                        - Domain verification
                        - Global configuration
```

### Patient-HCP Relationship Model (Future - Post-Alpha)

Patients must explicitly grant access to healthcare providers:

```csharp
// src/Infrastructure/Nocturne.Infrastructure.Data/Entities/PatientHcpAccessEntity.cs
/// <summary>
/// Represents a patient granting access to a healthcare provider
/// </summary>
public class PatientHcpAccessEntity
{
    public Guid Id { get; set; }
    
    /// <summary>Patient who granted access</summary>
    public Guid PatientSubjectId { get; set; }
    public SubjectEntity? PatientSubject { get; set; }
    
    /// <summary>Healthcare provider who received access</summary>
    public Guid HcpSubjectId { get; set; }
    public SubjectEntity? HcpSubject { get; set; }
    
    /// <summary>Verified domain of the HCP's organization</summary>
    public string HcpOrganizationDomain { get; set; } = string.Empty;
    
    /// <summary>Level of access: 'view', 'full', 'emergency'</summary>
    public string AccessLevel { get; set; } = "view";
    
    public DateTime GrantedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    
    /// <summary>How access was granted: 'qr_code', 'share_link', 'clinic_system', 'emergency'</summary>
    public string? GrantedVia { get; set; }
    
    public DateTime CreatedAt { get; set; }
}

// src/Infrastructure/Nocturne.Infrastructure.Data/Entities/PatientShareCodeEntity.cs
/// <summary>
/// Share codes for patients to grant access to HCPs
/// </summary>
public class PatientShareCodeEntity
{
    public Guid Id { get; set; }
    
    public Guid PatientSubjectId { get; set; }
    public SubjectEntity? PatientSubject { get; set; }
    
    /// <summary>Share code like "GLUC-ABCD-1234"</summary>
    public string Code { get; set; } = string.Empty;
    
    public string AccessLevel { get; set; } = "view";
    public int MaxUses { get; set; } = 1;
    public int UsesCount { get; set; } = 0;
    
    /// <summary>Restrict to specific hospital domains (optional)</summary>
    public List<string> AllowedDomains { get; set; } = new();
    
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### Patient Sharing Flow

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                     Patient Grants Access to HCP                                 │
└─────────────────────────────────────────────────────────────────────────────────┘

Option A: QR Code (In-Clinic)
─────────────────────────────
1. Patient opens Nocturne app → Settings → Share with Provider
2. App generates QR code with embedded share code
3. HCP scans QR code with their device
4. Access granted immediately (if HCP's domain is verified)

Option B: Share Link (Remote)
─────────────────────────────
1. Patient opens Nocturne app → Settings → Share with Provider
2. Patient enters HCP's email or selects hospital from list
3. Share link sent to HCP's work email
4. HCP clicks link, authenticates with work email
5. Access granted

Option C: Clinic Integration (Future)
─────────────────────────────────────
1. Patient checks in at clinic (via EHR integration)
2. Nocturne receives notification of appointment
3. Automatic temporary access granted for visit duration
4. Access revokes after 24 hours post-visit
```

### Self-Hosted OIDC (Enterprise Option)

For large health systems that prefer their own identity infrastructure:

```json
// domains/us/kaiser-permanente-org.json
{
  "domain": "kp.org",
  "organization": {
    "name": "Kaiser Permanente",
    "type": "health_system"
  },
  "authentication": {
    "method": "oidc_federation",
    "oidc_config": {
      "issuer_url": "https://sso.kp.org/realms/kp",
      "client_id": "nocturne-integration",
      // client_secret stored in Nocturne's secrets manager, not in repo
      "scopes": ["openid", "profile", "email"],
      "claim_mappings": {
        "email": "email",
        "roles": "kp_nocturne_roles"
      }
    }
  },
  "authorization": {
    "trust_provider_roles": true,  // Accept roles from their IdP
    "default_roles": ["hcp-viewer"]
  }
}
```

This allows Kaiser (or similar large systems) to:
- Use their existing employee directory
- Enforce their own MFA policies
- Manage role assignments in their IdP
- Deprovision access when employees leave

### Domain Sync Service (Future - Post-Alpha)

The domain sync service will pull verified healthcare provider domains from the GitHub repository:

```csharp
// src/Services/Nocturne.Services.Auth/DomainSyncService.cs
// Implementation deferred to post-alpha release
public class DomainSyncService : BackgroundService
{
    // Will sync verified domains from nightscout/nocturne-verified-domains
    // GitHub repo to enable plug-and-play HCP authentication
}
```

---

## 8. Security Considerations

### Token Security

| Concern | Mitigation |
|---------|------------|
| Token theft | Short TTL, secure cookies, token binding |
| Replay attacks | Nonce in token, single-use refresh tokens |
| Brute force | Rate limiting, account lockout |
| Token leakage | Opaque tokens, no sensitive data in token |

### Cookie Security

```csharp
var cookieOptions = new CookieOptions
{
    HttpOnly = true,
    Secure = true,
    SameSite = SameSiteMode.Strict,
    MaxAge = TimeSpan.FromHours(8),
    IsEssential = true
};
```

### API Secret Deprecation Path

1. **Phase 1**: Log warnings when `api-secret` used
2. **Phase 2**: Require explicit opt-in for `api-secret`
3. **Phase 3**: Default disable, require configuration flag
4. **Phase 4**: Remove in future major version

### Audit Requirements

All authentication events logged:
- Login success/failure
- Token issuance/revocation
- Permission changes
- Session creation/termination

---

## 9. Migration Strategy

### For Existing Nightscout Users

1. **Automatic Subject Migration**
   - Import existing subjects/roles from MongoDB
   - Generate new PostgreSQL UUIDs
   - Preserve access tokens (hash stored)
   - Maintain role assignments

2. **Gradual OIDC Adoption**
   - Legacy auth continues to work
   - Users can optionally link OIDC identity
   - Admin can require OIDC for specific roles

3. **Device Compatibility**
   - CGM uploaders continue using access tokens
   - Loop/OpenAPS use existing auth
   - Mobile apps get OIDC support

### Migration Commands

```bash
# Import subjects from MongoDB backup
dotnet run --project src/Tools/Nocturne.Tools.Migration -- \
  --command import-auth \
  --source mongodb://... \
  --target postgresql://...

# Generate new access tokens for all subjects
dotnet run --project src/Tools/Nocturne.Tools.Migration -- \
  --command regenerate-tokens \
  --target postgresql://...
```

---

## 10. Testing Strategy

### Unit Tests

```csharp
// TokenCacheServiceTests
[Fact]
public async Task GetAsync_CachedToken_ReturnsFromCache()
{
    // Arrange
    var entry = new TokenCacheEntry { TokenId = "test" };
    await _cache.SetAsync("hash", entry);
    
    // Act
    var result = await _service.GetAsync("hash");
    
    // Assert
    result.Should().BeEquivalentTo(entry);
}

// OidcTokenHandlerTests
[Fact]
public async Task AuthenticateAsync_ValidOidcToken_ReturnsSuccess()
{
    // Test OIDC token validation
}

// LegacyAccessTokenHandlerTests
[Fact]
public async Task AuthenticateAsync_ValidAccessToken_ReturnsSuccess()
{
    // Test Nightscout-style access token
}
```

### Integration Tests

```csharp
[Trait("Category", "Integration")]
public class AuthenticationIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task LegacyApiSecret_StillWorks()
    {
        // Test legacy api-secret authentication
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("api-secret", _hashedSecret);
        
        var response = await client.GetAsync("/api/v1/verifyauth");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
    
    [Fact]
    public async Task OidcToken_ExchangeAndAccess()
    {
        // Test full OIDC flow with test provider
    }
}
```

### E2E Tests (Playwright)

```typescript
test('login with OIDC provider', async ({ page }) => {
  await page.goto('/auth/login');
  await page.click('[data-testid="login-keycloak"]');
  
  // Handle Keycloak login page
  await page.fill('#username', 'testuser');
  await page.fill('#password', 'testpass');
  await page.click('#kc-login');
  
  // Should redirect back to app
  await expect(page).toHaveURL('/dashboard');
  await expect(page.locator('[data-testid="user-menu"]')).toContainText('testuser');
});
```

---

## Appendix A: API Endpoint Reference

### Authentication Endpoints

| Method | Path | Auth Required | Description |
|--------|------|---------------|-------------|
| GET | `/auth/login` | No | Initiate OIDC login |
| GET | `/auth/callback` | No | OIDC callback handler |
| POST | `/auth/logout` | Yes | Logout user |
| GET | `/auth/userinfo` | Yes | Get current user info |
| POST | `/auth/refresh` | Yes | Refresh access token |
| GET | `/auth/providers` | No | List available OIDC providers |

### Legacy Endpoints (Maintained)

| Method | Path | Auth Required | Description |
|--------|------|---------------|-------------|
| GET | `/api/v1/verifyauth` | Any | Verify authentication |
| GET | `/api/v2/authorization/request/:token` | No | Exchange access token for JWT |
| GET | `/api/v2/authorization/subjects` | Admin | List subjects |
| POST | `/api/v2/authorization/subjects` | Admin | Create subject |
| GET | `/api/v2/authorization/roles` | Admin | List roles |

### Token Management Endpoints (New)

| Method | Path | Auth Required | Description |
|--------|------|---------------|-------------|
| GET | `/api/v2/tokens` | Admin | List active tokens |
| POST | `/api/v2/tokens/revoke/:id` | Admin | Revoke specific token |
| POST | `/api/v2/tokens/revoke-all` | Admin | Revoke all tokens for subject |
| GET | `/api/v2/tokens/my` | User | List user's own tokens |

---

## Appendix B: Configuration Reference

```json
// appsettings.json
{
  "Authentication": {
    "ApiSecret": "your-api-secret-here",
    "JwtSecret": "your-jwt-secret-here",
    "TokenLifetimeMinutes": 60,
    "RefreshTokenLifetimeDays": 30,
    "AllowLegacyAuth": true,
    "RequireOidcForAdmin": false
  },
  "Oidc": {
    "Enabled": true,
    "DefaultProvider": "keycloak",
    "Providers": {
      "keycloak": {
        "IssuerUrl": "https://auth.example.com/realms/nocturne",
        "ClientId": "nocturne-web",
        "ClientSecret": "...",
        "Scopes": ["openid", "profile", "email"],
        "DefaultRoles": ["readable"]
      }
    }
  },
  "TokenCache": {
    "UseRedis": false,
    "RedisConnectionString": "localhost:6379",
    "LocalCacheSizeLimit": 10000,
    "DefaultTtlMinutes": 60
  }
}
```

---

## Appendix C: File Structure

```
src/
├── API/Nocturne.API/
│   ├── Controllers/
│   │   ├── OidcController.cs           # New OIDC endpoints
│   │   ├── TokenController.cs          # Token management
│   │   ├── AuthenticationController.cs # Updated
│   │   └── AuthorizationController.cs  # Updated
│   ├── Middleware/
│   │   ├── AuthenticationMiddleware.cs # Refactored
│   │   └── Handlers/
│   │       ├── IAuthHandler.cs
│   │       ├── OidcTokenHandler.cs
│   │       ├── LegacyJwtHandler.cs
│   │       ├── AccessTokenHandler.cs
│   │       └── ApiSecretHandler.cs
│   └── Services/
│       └── AuthorizationService.cs     # Updated
├── Core/
│   ├── Nocturne.Core.Contracts/
│   │   ├── ITokenService.cs            # New
│   │   ├── IOidcProviderService.cs     # New
│   │   └── IAuthorizationService.cs    # Updated
│   └── Nocturne.Core.Models/
│       ├── Token.cs                    # New
│       ├── TokenCacheEntry.cs          # New
│       ├── OidcProvider.cs             # New
│       └── Authorization.cs            # Updated
├── Infrastructure/
│   ├── Nocturne.Infrastructure.Cache/
│   │   └── Services/
│   │       ├── ITokenCacheService.cs
│   │       ├── MemoryTokenCacheService.cs
│   │       └── DistributedTokenCacheService.cs
│   └── Nocturne.Infrastructure.Data/
│       ├── Entities/
│       │   ├── TokenEntity.cs
│       │   ├── SubjectEntity.cs
│       │   ├── RoleEntity.cs
│       │   ├── SubjectRoleEntity.cs
│       │   ├── OidcProviderEntity.cs
│       │   └── AuthAuditLogEntity.cs
│       └── Repositories/
│           ├── ITokenRepository.cs
│           ├── ISubjectRepository.cs
│           └── IRoleRepository.cs
└── Web/packages/app/
    └── src/
        ├── lib/stores/auth.ts
        ├── hooks.server.ts
        └── routes/
            ├── auth/
            │   ├── login/+page.svelte
            │   ├── logout/+page.server.ts
            │   └── callback/+page.server.ts
            └── (protected)/
                └── +layout.server.ts
```

---

## Summary

This implementation plan provides a comprehensive approach to adding OIDC authentication to Nocturne while maintaining full backward compatibility with Nightscout's legacy authentication systems. Key highlights:

1. **Opaque tokens** with server-side validation for immediate revocability
2. **Multi-layer caching** (L1 memory + L2 Redis) to avoid introspection latency
3. **Handler-based auth pipeline** for clean separation of auth methods
4. **Full legacy support** for CGM devices, uploaders, and existing integrations
5. **SSO-ready architecture** with templates for common healthcare providers
6. **Gradual migration path** from legacy auth to modern OIDC

The 6-7 week implementation timeline allows for thorough testing and validation at each phase, ensuring stability for the critical healthcare use case.
