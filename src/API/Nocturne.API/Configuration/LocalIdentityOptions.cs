namespace Nocturne.API.Configuration;

/// <summary>
/// Configuration options for the built-in local identity provider
/// Enables Nocturne to function as its own OAuth2/OIDC issuer without external providers
/// </summary>
public class LocalIdentityOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "LocalIdentity";

    /// <summary>
    /// Whether the local identity provider is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Display name for the local identity provider in login UI
    /// </summary>
    public string DisplayName { get; set; } = "Nocturne";

    /// <summary>
    /// Registration settings
    /// </summary>
    public RegistrationSettings Registration { get; set; } = new();

    /// <summary>
    /// Email allowlist configuration
    /// </summary>
    public AllowlistSettings Allowlist { get; set; } = new();

    /// <summary>
    /// Password requirements
    /// </summary>
    public PasswordSettings Password { get; set; } = new();

    /// <summary>
    /// Account lockout settings
    /// </summary>
    public LockoutSettings Lockout { get; set; } = new();

    /// <summary>
    /// Token lifetime settings
    /// </summary>
    public TokenSettings Tokens { get; set; } = new();

    /// <summary>
    /// Admin account to seed on startup (optional)
    /// If set, this account will be created automatically if it doesn't exist
    /// </summary>
    public AdminSeedOptions? AdminSeed { get; set; }
}

/// <summary>
/// Options for seeding an initial admin account
/// </summary>
public class AdminSeedOptions
{
    /// <summary>
    /// Admin email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Admin password (only used for initial seeding)
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Admin display name
    /// </summary>
    public string? DisplayName { get; set; } = "Admin";
}

/// <summary>
/// Registration configuration
/// </summary>
public class RegistrationSettings
{
    /// <summary>
    /// Whether new user registration is allowed
    /// </summary>
    public bool AllowRegistration { get; set; } = true;

    /// <summary>
    /// Whether email verification is required before login
    /// </summary>
    public bool RequireEmailVerification { get; set; } = true;

    /// <summary>
    /// Whether admin approval is required for new registrations
    /// </summary>
    public bool RequireAdminApproval { get; set; } = false;

    /// <summary>
    /// Default roles to assign to new users
    /// </summary>
    public List<string> DefaultRoles { get; set; } = new() { "readable" };
}

/// <summary>
/// Email/domain allowlist settings
/// </summary>
public class AllowlistSettings
{
    /// <summary>
    /// Whether the allowlist is enabled
    /// If enabled, only allowed emails/domains can register
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// List of allowed email addresses (exact match)
    /// Example: ["admin@example.com", "user@example.com"]
    /// </summary>
    public List<string> AllowedEmails { get; set; } = new();

    /// <summary>
    /// List of allowed email domains
    /// Example: ["example.com", "hospital.org"]
    /// </summary>
    public List<string> AllowedDomains { get; set; } = new();

    /// <summary>
    /// If true, non-allowlisted users can still register but require admin approval
    /// If false, non-allowlisted users are rejected immediately
    /// </summary>
    public bool AllowOthersWithApproval { get; set; } = false;
}

/// <summary>
/// Password requirements
/// </summary>
public class PasswordSettings
{
    /// <summary>
    /// Minimum password length
    /// </summary>
    public int MinLength { get; set; } = 12;

    /// <summary>
    /// Maximum password length
    /// </summary>
    public int MaxLength { get; set; } = 128;

    /// <summary>
    /// Require at least one uppercase letter
    /// </summary>
    public bool RequireUppercase { get; set; } = false;

    /// <summary>
    /// Require at least one lowercase letter
    /// </summary>
    public bool RequireLowercase { get; set; } = false;

    /// <summary>
    /// Require at least one digit
    /// </summary>
    public bool RequireDigit { get; set; } = false;

    /// <summary>
    /// Require at least one special character
    /// </summary>
    public bool RequireSpecialCharacter { get; set; } = false;
}

/// <summary>
/// Account lockout settings
/// </summary>
public class LockoutSettings
{
    /// <summary>
    /// Whether account lockout is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of failed attempts before lockout
    /// </summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>
    /// Initial lockout duration in minutes
    /// </summary>
    public int LockoutDurationMinutes { get; set; } = 5;

    /// <summary>
    /// Whether lockout duration should increase exponentially
    /// </summary>
    public bool ExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Maximum lockout duration in minutes when using exponential backoff
    /// </summary>
    public int MaxLockoutDurationMinutes { get; set; } = 60;
}

/// <summary>
/// Token lifetime settings for local identity provider
/// </summary>
public class TokenSettings
{
    /// <summary>
    /// Email verification token lifetime in hours
    /// </summary>
    public int EmailVerificationTokenHours { get; set; } = 24;

    /// <summary>
    /// Password reset token lifetime in hours
    /// </summary>
    public int PasswordResetTokenHours { get; set; } = 1;
}
