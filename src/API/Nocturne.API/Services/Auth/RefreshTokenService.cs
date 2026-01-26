using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models.Configuration;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services.Auth;

/// <summary>
/// Service for managing refresh tokens stored in the database
/// </summary>
public class RefreshTokenService : IRefreshTokenService
{
    private readonly NocturneDbContext _dbContext;
    private readonly IJwtService _jwtService;
    private readonly JwtOptions _options;
    private readonly ILogger<RefreshTokenService> _logger;

    /// <summary>
    /// Creates a new instance of RefreshTokenService
    /// </summary>
    public RefreshTokenService(
        NocturneDbContext dbContext,
        IJwtService jwtService,
        IOptions<JwtOptions> options,
        ILogger<RefreshTokenService> logger)
    {
        _dbContext = dbContext;
        _jwtService = jwtService;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> CreateRefreshTokenAsync(
        Guid subjectId,
        string? oidcSessionId = null,
        string? deviceDescription = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        var refreshToken = _jwtService.GenerateRefreshToken();
        var tokenHash = _jwtService.HashRefreshToken(refreshToken);

        var entity = new RefreshTokenEntity
        {
            Id = Guid.CreateVersion7(),
            TokenHash = tokenHash,
            SubjectId = subjectId,
            OidcSessionId = oidcSessionId,
            DeviceDescription = deviceDescription,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenLifetimeDays),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.RefreshTokens.Add(entity);
        await _dbContext.SaveChangesAsync();

        _logger.LogDebug("Created refresh token for subject {SubjectId}", subjectId);

        return refreshToken;
    }

    /// <inheritdoc />
    public async Task<Guid?> ValidateRefreshTokenAsync(string refreshToken)
    {
        var tokenHash = _jwtService.HashRefreshToken(refreshToken);

        var entity = await _dbContext.RefreshTokens
            .AsNoTracking()
            .Where(t => t.TokenHash == tokenHash)
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            _logger.LogDebug("Refresh token not found");
            return null;
        }

        if (entity.IsRevoked)
        {
            _logger.LogWarning(
                "Attempt to use revoked refresh token {TokenId} for subject {SubjectId}",
                entity.Id, entity.SubjectId);
            return null;
        }

        if (entity.IsExpired)
        {
            _logger.LogDebug("Refresh token {TokenId} has expired", entity.Id);
            return null;
        }

        return entity.SubjectId;
    }

    /// <inheritdoc />
    public async Task<string?> RotateRefreshTokenAsync(
        string oldRefreshToken,
        string? ipAddress = null,
        string? userAgent = null)
    {
        var tokenHash = _jwtService.HashRefreshToken(oldRefreshToken);

        var oldEntity = await _dbContext.RefreshTokens
            .Where(t => t.TokenHash == tokenHash)
            .FirstOrDefaultAsync();

        if (oldEntity == null || !oldEntity.IsValid)
        {
            if (oldEntity != null && oldEntity.IsRevoked && oldEntity.ReplacedByTokenId.HasValue)
            {
                // Token reuse detected - this could be a token theft attempt
                // Revoke all tokens in the family
                _logger.LogWarning(
                    "Refresh token reuse detected for subject {SubjectId}. Revoking all tokens in the family.",
                    oldEntity.SubjectId);

                await RevokeTokenFamilyAsync(oldEntity.SubjectId, "Token reuse detected");
            }
            return null;
        }

        // Create new refresh token
        var newRefreshToken = _jwtService.GenerateRefreshToken();
        var newTokenHash = _jwtService.HashRefreshToken(newRefreshToken);

        var newEntity = new RefreshTokenEntity
        {
            Id = Guid.CreateVersion7(),
            TokenHash = newTokenHash,
            SubjectId = oldEntity.SubjectId,
            OidcSessionId = oldEntity.OidcSessionId,
            DeviceDescription = oldEntity.DeviceDescription,
            IpAddress = ipAddress ?? oldEntity.IpAddress,
            UserAgent = userAgent ?? oldEntity.UserAgent,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenLifetimeDays),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Revoke old token and link to new one
        oldEntity.RevokedAt = DateTime.UtcNow;
        oldEntity.RevokedReason = "Rotated";
        oldEntity.ReplacedByTokenId = newEntity.Id;
        oldEntity.UpdatedAt = DateTime.UtcNow;

        _dbContext.RefreshTokens.Add(newEntity);
        await _dbContext.SaveChangesAsync();

        _logger.LogDebug(
            "Rotated refresh token for subject {SubjectId}. Old: {OldTokenId}, New: {NewTokenId}",
            oldEntity.SubjectId, oldEntity.Id, newEntity.Id);

        return newRefreshToken;
    }

    /// <inheritdoc />
    public async Task<bool> RevokeRefreshTokenAsync(string refreshToken, string reason)
    {
        var tokenHash = _jwtService.HashRefreshToken(refreshToken);

        var entity = await _dbContext.RefreshTokens
            .Where(t => t.TokenHash == tokenHash)
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            return false;
        }

        if (entity.IsRevoked)
        {
            return true; // Already revoked
        }

        entity.RevokedAt = DateTime.UtcNow;
        entity.RevokedReason = reason;
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Revoked refresh token {TokenId} for subject {SubjectId}. Reason: {Reason}",
            entity.Id, entity.SubjectId, reason);

        return true;
    }

    /// <inheritdoc />
    public async Task<int> RevokeAllRefreshTokensForSubjectAsync(Guid subjectId, string reason)
    {
        var tokens = await _dbContext.RefreshTokens
            .Where(t => t.SubjectId == subjectId && t.RevokedAt == null)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var token in tokens)
        {
            token.RevokedAt = now;
            token.RevokedReason = reason;
            token.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Revoked {Count} refresh tokens for subject {SubjectId}. Reason: {Reason}",
            tokens.Count, subjectId, reason);

        return tokens.Count;
    }

    /// <inheritdoc />
    public async Task<int> RevokeRefreshTokensByOidcSessionAsync(string oidcSessionId, string reason)
    {
        var tokens = await _dbContext.RefreshTokens
            .Where(t => t.OidcSessionId == oidcSessionId && t.RevokedAt == null)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var token in tokens)
        {
            token.RevokedAt = now;
            token.RevokedReason = reason;
            token.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Revoked {Count} refresh tokens for OIDC session {SessionId}. Reason: {Reason}",
            tokens.Count, oidcSessionId, reason);

        return tokens.Count;
    }

    /// <inheritdoc />
    public async Task<List<RefreshTokenInfo>> GetActiveSessionsForSubjectAsync(Guid subjectId)
    {
        var tokens = await _dbContext.RefreshTokens
            .AsNoTracking()
            .Where(t => t.SubjectId == subjectId && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(t => t.LastUsedAt ?? t.IssuedAt)
            .Select(t => new RefreshTokenInfo
            {
                Id = t.Id,
                DeviceDescription = t.DeviceDescription,
                IpAddress = t.IpAddress,
                IssuedAt = t.IssuedAt,
                LastUsedAt = t.LastUsedAt,
                ExpiresAt = t.ExpiresAt,
                IsCurrent = false // Will be set by caller
            })
            .ToListAsync();

        return tokens;
    }

    /// <inheritdoc />
    public async Task UpdateLastUsedAsync(string refreshToken)
    {
        var tokenHash = _jwtService.HashRefreshToken(refreshToken);

        await _dbContext.RefreshTokens
            .Where(t => t.TokenHash == tokenHash)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.LastUsedAt, DateTime.UtcNow)
                .SetProperty(t => t.UpdatedAt, DateTime.UtcNow));
    }

    /// <inheritdoc />
    public async Task<int> PruneExpiredRefreshTokensAsync(DateTime? olderThan = null)
    {
        var cutoffDate = olderThan ?? DateTime.UtcNow.AddDays(-30); // Keep revoked tokens for 30 days by default

        var count = await _dbContext.RefreshTokens
            .Where(t => t.ExpiresAt < cutoffDate || (t.RevokedAt != null && t.RevokedAt < cutoffDate))
            .ExecuteDeleteAsync();

        if (count > 0)
        {
            _logger.LogInformation("Pruned {Count} expired/old refresh tokens", count);
        }

        return count;
    }

    /// <summary>
    /// Revoke all tokens in a token family (all tokens for a subject)
    /// Used when token reuse is detected
    /// </summary>
    private async Task RevokeTokenFamilyAsync(Guid subjectId, string reason)
    {
        await RevokeAllRefreshTokensForSubjectAsync(subjectId, reason);
    }
}
