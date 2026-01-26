using System.ComponentModel.DataAnnotations;

namespace Nocturne.Core.Models.Configuration;

/// <summary>
/// Configuration options for local identity (username/password) authentication.
/// </summary>
public class LocalIdentityOptions
{
    public const string SectionName = "LocalIdentity";

    /// <summary>
    /// Whether local identity authentication is enabled.
    /// When disabled, only external providers (OIDC) can be used.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Display name shown on the login form for local authentication.
    /// </summary>
    public string DisplayName { get; set; } = "Local Account";

    /// <summary>
    /// Registration settings controlling how new users can sign up.
    /// </summary>
    public RegistrationSettings Registration { get; set; } = new();

    /// <summary>
    /// Allowlist settings for restricting who can register.
    /// </summary>
    public AllowlistSettings Allowlist { get; set; } = new();

    /// <summary>
    /// Password policy configuration.
    /// </summary>
    public PasswordSettings Password { get; set; } = new();

    /// <summary>
    /// Account lockout settings for brute-force protection.
    /// </summary>
    public LockoutSettings Lockout { get; set; } = new();

    /// <summary>
    /// Token lifetime settings for verification and password reset.
    /// </summary>
    public TokenSettings Tokens { get; set; } = new();

    /// <summary>
    /// Optional list of users to seed on startup.
    /// Useful for creating initial admin accounts.
    /// </summary>
    public List<SeedUserOptions> SeedUsers { get; set; } = new();
}

/// <summary>
/// Settings controlling user registration.
/// </summary>
public class RegistrationSettings
{
    /// <summary>
    /// Whether new user registration is allowed.
    /// If false, only seeded users or admin-created accounts can log in.
    /// </summary>
    public bool AllowRegistration { get; set; } = true;

    /// <summary>
    /// Whether email verification is required before login.
    /// If true, users must click a verification link sent to their email.
    /// </summary>
    public bool RequireEmailVerification { get; set; } = true;

    /// <summary>
    /// Whether admin approval is required after registration.
    /// If true, users can verify their email but cannot log in until approved.
    /// </summary>
    public bool RequireAdminApproval { get; set; } = false;

    /// <summary>
    /// Default roles assigned to new users upon registration.
    /// </summary>
    public List<string> DefaultRoles { get; set; } = new() { "readable" };
}

/// <summary>
/// Settings for restricting registration to specific emails/domains.
/// </summary>
public class AllowlistSettings
{
    /// <summary>
    /// Whether the allowlist is enabled.
    /// If false, anyone can register (subject to other settings).
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// List of exact email addresses allowed to register.
    /// </summary>
    public List<string> AllowedEmails { get; set; } = new();

    /// <summary>
    /// List of email domains allowed to register (e.g., "company.com").
    /// </summary>
    public List<string> AllowedDomains { get; set; } = new();

    /// <summary>
    /// If true, users not on the allowlist can still register but require admin approval.
    /// If false, users not on the allowlist are completely blocked from registration.
    /// </summary>
    public bool AllowOthersWithApproval { get; set; } = false;
}

/// <summary>
/// Password policy settings.
/// </summary>
public class PasswordSettings
{
    /// <summary>
    /// Minimum password length.
    /// </summary>
    [Range(4, 128)]
    public int MinLength { get; set; } = 8;

    /// <summary>
    /// Maximum password length.
    /// </summary>
    [Range(8, 1024)]
    public int MaxLength { get; set; } = 128;

    /// <summary>
    /// Whether at least one uppercase letter is required.
    /// </summary>
    public bool RequireUppercase { get; set; } = true;

    /// <summary>
    /// Whether at least one lowercase letter is required.
    /// </summary>
    public bool RequireLowercase { get; set; } = true;

    /// <summary>
    /// Whether at least one digit is required.
    /// </summary>
    public bool RequireDigit { get; set; } = true;

    /// <summary>
    /// Whether at least one special character is required.
    /// </summary>
    public bool RequireSpecialCharacter { get; set; } = false;

    /// <summary>
    /// List of special characters that satisfy the special character requirement.
    /// </summary>
    public string SpecialCharacters { get; set; } = "!@#$%^&*()_+-=[]{}|;':\",./<>?";
}

/// <summary>
/// Account lockout settings for brute-force protection.
/// </summary>
public class LockoutSettings
{
    /// <summary>
    /// Whether account lockout is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of failed login attempts before lockout.
    /// </summary>
    [Range(1, 100)]
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>
    /// Initial lockout duration in minutes.
    /// </summary>
    [Range(1, 1440)]
    public int LockoutDurationMinutes { get; set; } = 5;

    /// <summary>
    /// Whether to use exponential backoff for repeated lockouts.
    /// Each subsequent lockout doubles the duration.
    /// </summary>
    public bool ExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Maximum lockout duration in minutes when using exponential backoff.
    /// </summary>
    [Range(1, 10080)]
    public int MaxLockoutDurationMinutes { get; set; } = 60;
}

/// <summary>
/// Token lifetime settings.
/// </summary>
public class TokenSettings
{
    /// <summary>
    /// How long email verification tokens are valid, in hours.
    /// </summary>
    [Range(1, 168)]
    public int EmailVerificationTokenHours { get; set; } = 24;

    /// <summary>
    /// How long password reset tokens are valid, in hours.
    /// </summary>
    [Range(1, 24)]
    public int PasswordResetTokenHours { get; set; } = 1;
}

/// <summary>
/// Configuration for a user to be seeded on startup.
/// </summary>
public class SeedUserOptions
{
    /// <summary>
    /// Email address for the seeded user (also used as username).
    /// </summary>
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Password for the seeded user.
    /// </summary>
    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the seeded user.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Whether this user should be an admin.
    /// </summary>
    public bool IsAdmin { get; set; } = false;

    /// <summary>
    /// Additional roles to assign to this user.
    /// </summary>
    public List<string> Roles { get; set; } = new();
}
