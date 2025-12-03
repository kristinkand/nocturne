namespace Nocturne.API.Configuration;

/// <summary>
/// Email sending configuration
/// Separate from LocalIdentity so SMTP can be used for other notifications
/// </summary>
public class EmailOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "Email";

    /// <summary>
    /// Whether SMTP is configured and email sending is enabled
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// SMTP server hostname
    /// </summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>
    /// SMTP server port
    /// </summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// Use SSL/TLS for SMTP connection
    /// </summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>
    /// SMTP username (if authentication required)
    /// </summary>
    public string? SmtpUsername { get; set; }

    /// <summary>
    /// SMTP password (if authentication required)
    /// </summary>
    public string? SmtpPassword { get; set; }

    /// <summary>
    /// From email address
    /// </summary>
    public string FromAddress { get; set; } = "noreply@example.com";

    /// <summary>
    /// From display name
    /// </summary>
    public string FromName { get; set; } = "Nocturne";

    /// <summary>
    /// Admin email address for notifications when SMTP is not configured
    /// Password reset requests will be logged for admin to handle manually
    /// </summary>
    public string? AdminEmail { get; set; }

    /// <summary>
    /// Base URL for email links (if not set, auto-detected from request)
    /// </summary>
    public string? BaseUrl { get; set; }
}
