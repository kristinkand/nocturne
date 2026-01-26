using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models.Configuration;

namespace Nocturne.API.Services.Auth;

/// <summary>
/// Email service implementation with SMTP sending and admin notification fallback
/// When SMTP is not configured, emails are logged and admin is notified for manual handling
/// </summary>
public class EmailService : IEmailService
{
    private readonly EmailOptions _settings;
    private readonly ILogger<EmailService> _logger;

    /// <summary>
    /// Creates a new instance of EmailService
    /// </summary>
    public EmailService(IOptions<EmailOptions> options, ILogger<EmailService> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _settings.Enabled && !string.IsNullOrEmpty(_settings.SmtpHost);

    /// <inheritdoc />
    public async Task<EmailSendResult> SendAsync(
        string to,
        string subject,
        string htmlBody,
        string? textBody = null
    )
    {
        if (!IsEnabled)
        {
            _logger.LogInformation(
                "Email sending disabled. Would send to {To}: {Subject}\n{Body}",
                to,
                subject,
                textBody ?? htmlBody
            );
            return EmailSendResult.Logged();
        }

        try
        {
            using var client = CreateSmtpClient();
            using var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, _settings.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
            };

            message.To.Add(new MailAddress(to));

            if (!string.IsNullOrEmpty(textBody))
            {
                message.AlternateViews.Add(
                    AlternateView.CreateAlternateViewFromString(textBody, null, "text/plain")
                );
            }

            await client.SendMailAsync(message);

            _logger.LogDebug("Email sent successfully to {To}: {Subject}", to, subject);
            return EmailSendResult.Sent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}: {Subject}", to, subject);
            return EmailSendResult.Failed(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<EmailSendResult> SendEmailVerificationAsync(
        string to,
        string? displayName,
        string verificationUrl
    )
    {
        var name = displayName ?? to.Split('@').FirstOrDefault() ?? "User";
        var encodedName = WebUtility.HtmlEncode(name);
        var encodedUrl = WebUtility.HtmlEncode(verificationUrl);

        var subject = "Verify your email address - Nocturne";

        var htmlBody = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <style>
                    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }
                    .button { display: inline-block; padding: 12px 24px; background: #4f46e5; color: white; text-decoration: none; border-radius: 6px; font-weight: 600; }
                    .footer { margin-top: 40px; padding-top: 20px; border-top: 1px solid #eee; font-size: 12px; color: #666; }
                </style>
            </head>
            <body>
                <h1>Verify your email address</h1>
                <p>Hi {{encodedName}},</p>
                <p>Thanks for signing up for Nocturne! Please click the button below to verify your email address:</p>
                <p style="margin: 30px 0;">
                    <a href="{{encodedUrl}}" class="button">Verify Email</a>
                </p>
                <p>Or copy and paste this link into your browser:</p>
                <p style="word-break: break-all; color: #666;">{{encodedUrl}}</p>
                <p>This link will expire in 24 hours.</p>
                <p>If you didn't create an account, you can safely ignore this email.</p>
                <div class="footer">
                    <p>This email was sent by Nocturne.</p>
                </div>
            </body>
            </html>
            """;

        var textBody = $"""
            Verify your email address

            Hi {name},

            Thanks for signing up for Nocturne! Please click the link below to verify your email address:

            {verificationUrl}

            This link will expire in 24 hours.

            If you didn't create an account, you can safely ignore this email.

            --
            This email was sent by Nocturne.
            """;

        return await SendAsync(to, subject, htmlBody, textBody);
    }

    /// <inheritdoc />
    public async Task<EmailSendResult> SendPasswordResetAsync(
        string to,
        string? displayName,
        string resetUrl
    )
    {
        var name = displayName ?? to.Split('@').FirstOrDefault() ?? "User";
        var encodedName = WebUtility.HtmlEncode(name);
        var encodedUrl = WebUtility.HtmlEncode(resetUrl);

        var subject = "Reset your password - Nocturne";

        var htmlBody = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <style>
                    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }
                    .button { display: inline-block; padding: 12px 24px; background: #4f46e5; color: white; text-decoration: none; border-radius: 6px; font-weight: 600; }
                    .footer { margin-top: 40px; padding-top: 20px; border-top: 1px solid #eee; font-size: 12px; color: #666; }
                    .warning { background: #fef3c7; border: 1px solid #f59e0b; padding: 12px; border-radius: 6px; margin: 20px 0; }
                </style>
            </head>
            <body>
                <h1>Reset your password</h1>
                <p>Hi {{encodedName}},</p>
                <p>We received a request to reset your password. Click the button below to choose a new password:</p>
                <p style="margin: 30px 0;">
                    <a href="{{encodedUrl}}" class="button">Reset Password</a>
                </p>
                <p>Or copy and paste this link into your browser:</p>
                <p style="word-break: break-all; color: #666;">{{encodedUrl}}</p>
                <div class="warning">
                    <strong>This link will expire in 1 hour.</strong>
                </div>
                <p>If you didn't request a password reset, you can safely ignore this email. Your password will remain unchanged.</p>
                <div class="footer">
                    <p>This email was sent by Nocturne.</p>
                </div>
            </body>
            </html>
            """;

        var textBody = $"""
            Reset your password

            Hi {name},

            We received a request to reset your password. Click the link below to choose a new password:

            {resetUrl}

            This link will expire in 1 hour.

            If you didn't request a password reset, you can safely ignore this email. Your password will remain unchanged.

            --
            This email was sent by Nocturne.
            """;

        return await SendAsync(to, subject, htmlBody, textBody);
    }

    /// <inheritdoc />
    public async Task<EmailSendResult> SendWelcomeAsync(string to, string? displayName)
    {
        var name = displayName ?? to.Split('@').FirstOrDefault() ?? "User";
        var encodedName = WebUtility.HtmlEncode(name);

        var subject = "Welcome to Nocturne!";

        var htmlBody = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <style>
                    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }
                    .footer { margin-top: 40px; padding-top: 20px; border-top: 1px solid #eee; font-size: 12px; color: #666; }
                </style>
            </head>
            <body>
                <h1>Welcome to Nocturne!</h1>
                <p>Hi {{encodedName}},</p>
                <p>Your email has been verified and your account is now active.</p>
                <p>You can now log in to access your diabetes management data.</p>
                <div class="footer">
                    <p>This email was sent by Nocturne.</p>
                </div>
            </body>
            </html>
            """;

        var textBody = $"""
            Welcome to Nocturne!

            Hi {name},

            Your email has been verified and your account is now active.

            You can now log in to access your diabetes management data.

            --
            This email was sent by Nocturne.
            """;

        return await SendAsync(to, subject, htmlBody, textBody);
    }

    /// <inheritdoc />
    public async Task<EmailSendResult> SendAdminNewUserNotificationAsync(
        string userEmail,
        string? displayName
    )
    {
        if (string.IsNullOrEmpty(_settings.AdminEmail))
        {
            _logger.LogWarning(
                "New user registration pending approval but no admin email configured. User: {Email} ({DisplayName})",
                userEmail,
                displayName ?? "no name"
            );
            return EmailSendResult.Logged();
        }

        var encodedEmail = WebUtility.HtmlEncode(userEmail);
        var encodedDisplayName = WebUtility.HtmlEncode(displayName ?? "Not provided");

        var subject = "New user registration pending approval - Nocturne";

        var htmlBody = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <style>
                    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }
                    .info { background: #f3f4f6; padding: 16px; border-radius: 6px; margin: 20px 0; }
                    .footer { margin-top: 40px; padding-top: 20px; border-top: 1px solid #eee; font-size: 12px; color: #666; }
                </style>
            </head>
            <body>
                <h1>New User Registration</h1>
                <p>A new user has registered and requires your approval:</p>
                <div class="info">
                    <p><strong>Email:</strong> {{encodedEmail}}</p>
                    <p><strong>Display Name:</strong> {{encodedDisplayName}}</p>
                </div>
                <p>Please log in to the admin panel to approve or reject this registration.</p>
                <div class="footer">
                    <p>This notification was sent by Nocturne.</p>
                </div>
            </body>
            </html>
            """;

        var textBody = $"""
            New User Registration

            A new user has registered and requires your approval:

            Email: {userEmail}
            Display Name: {displayName ?? "Not provided"}

            Please log in to the admin panel to approve or reject this registration.

            --
            This notification was sent by Nocturne.
            """;

        var result = await SendAsync(_settings.AdminEmail, subject, htmlBody, textBody);
        if (result.Success)
        {
            return EmailSendResult.AdminNotificationSent();
        }
        return result;
    }

    /// <inheritdoc />
    public async Task<EmailSendResult> SendAdminPasswordResetRequestNotificationAsync(
        string userEmail,
        string? displayName,
        Guid requestId
    )
    {
        if (string.IsNullOrEmpty(_settings.AdminEmail))
        {
            _logger.LogWarning(
                "Password reset request requires admin action but no admin email configured. User: {Email}, RequestId: {RequestId}",
                userEmail,
                requestId
            );
            return EmailSendResult.Logged();
        }

        var encodedEmail = WebUtility.HtmlEncode(userEmail);
        var encodedDisplayName = WebUtility.HtmlEncode(displayName ?? "Not provided");

        var subject = "Password reset request requires attention - Nocturne";

        var htmlBody = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <style>
                    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }
                    .info { background: #f3f4f6; padding: 16px; border-radius: 6px; margin: 20px 0; }
                    .warning { background: #fef3c7; border: 1px solid #f59e0b; padding: 12px; border-radius: 6px; margin: 20px 0; }
                    .footer { margin-top: 40px; padding-top: 20px; border-top: 1px solid #eee; font-size: 12px; color: #666; }
                </style>
            </head>
            <body>
                <h1>Password Reset Request</h1>
                <div class="warning">
                    <strong>Email sending is not configured.</strong> Manual action required.
                </div>
                <p>A user has requested a password reset but emails cannot be sent automatically:</p>
                <div class="info">
                    <p><strong>Email:</strong> {{encodedEmail}}</p>
                    <p><strong>Display Name:</strong> {{encodedDisplayName}}</p>
                    <p><strong>Request ID:</strong> {{requestId}}</p>
                </div>
                <p>Please log in to the admin panel to handle this request. You can generate a password reset link to share with the user securely.</p>
                <div class="footer">
                    <p>This notification was sent by Nocturne.</p>
                </div>
            </body>
            </html>
            """;

        var textBody = $"""
            Password Reset Request

            Email sending is not configured. Manual action required.

            A user has requested a password reset but emails cannot be sent automatically:

            Email: {userEmail}
            Display Name: {displayName ?? "Not provided"}
            Request ID: {requestId}

            Please log in to the admin panel to handle this request. You can generate a password reset link to share with the user securely.

            --
            This notification was sent by Nocturne.
            """;

        var result = await SendAsync(_settings.AdminEmail, subject, htmlBody, textBody);
        if (result.Success)
        {
            return EmailSendResult.AdminNotificationSent();
        }
        return result;
    }

    private SmtpClient CreateSmtpClient()
    {
        var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
        {
            EnableSsl = _settings.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        if (!string.IsNullOrEmpty(_settings.SmtpUsername))
        {
            client.Credentials = new NetworkCredential(
                _settings.SmtpUsername,
                _settings.SmtpPassword
            );
        }

        return client;
    }
}
