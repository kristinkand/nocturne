namespace Nocturne.Core.Contracts;

/// <summary>
/// Service for sending emails
/// Falls back to admin notification/logging when SMTP is not configured
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Whether email sending is enabled (SMTP configured)
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Send an email
    /// </summary>
    /// <param name="to">Recipient email address</param>
    /// <param name="subject">Email subject</param>
    /// <param name="htmlBody">HTML body content</param>
    /// <param name="textBody">Plain text body content (optional)</param>
    /// <returns>Result of sending attempt</returns>
    Task<EmailSendResult> SendAsync(
        string to,
        string subject,
        string htmlBody,
        string? textBody = null
    );

    /// <summary>
    /// Send an email verification email
    /// </summary>
    /// <param name="to">Recipient email address</param>
    /// <param name="displayName">User's display name</param>
    /// <param name="verificationUrl">URL to verify email</param>
    /// <returns>Result of sending attempt</returns>
    Task<EmailSendResult> SendEmailVerificationAsync(
        string to,
        string? displayName,
        string verificationUrl
    );

    /// <summary>
    /// Send a password reset email
    /// </summary>
    /// <param name="to">Recipient email address</param>
    /// <param name="displayName">User's display name</param>
    /// <param name="resetUrl">URL to reset password</param>
    /// <returns>Result of sending attempt</returns>
    Task<EmailSendResult> SendPasswordResetAsync(string to, string? displayName, string resetUrl);

    /// <summary>
    /// Send a welcome email after registration
    /// </summary>
    /// <param name="to">Recipient email address</param>
    /// <param name="displayName">User's display name</param>
    /// <returns>Result of sending attempt</returns>
    Task<EmailSendResult> SendWelcomeAsync(string to, string? displayName);

    /// <summary>
    /// Send admin notification about a new registration pending approval
    /// </summary>
    /// <param name="userEmail">Email of the user who registered</param>
    /// <param name="displayName">Display name of the user</param>
    /// <returns>Result of sending attempt</returns>
    Task<EmailSendResult> SendAdminNewUserNotificationAsync(string userEmail, string? displayName);

    /// <summary>
    /// Send admin notification about a password reset request (when SMTP is not configured)
    /// </summary>
    /// <param name="userEmail">Email of the user who requested reset</param>
    /// <param name="displayName">Display name of the user</param>
    /// <param name="requestId">ID of the password reset request</param>
    /// <returns>Result of sending attempt</returns>
    Task<EmailSendResult> SendAdminPasswordResetRequestNotificationAsync(
        string userEmail,
        string? displayName,
        Guid requestId
    );
}

/// <summary>
/// Result of an email send attempt
/// </summary>
public class EmailSendResult
{
    /// <summary>
    /// Whether the email was sent successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if sending failed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Whether the email was logged instead of sent (SMTP not configured)
    /// </summary>
    public bool LoggedOnly { get; set; }

    /// <summary>
    /// Whether admin was notified instead of sending to user (for password resets when SMTP not configured)
    /// </summary>
    public bool AdminNotified { get; set; }

    public static EmailSendResult Sent() => new() { Success = true };

    public static EmailSendResult Logged() => new() { Success = true, LoggedOnly = true };

    public static EmailSendResult AdminNotificationSent() =>
        new() { Success = true, AdminNotified = true };

    public static EmailSendResult Failed(string error) => new() { Success = false, Error = error };
}
