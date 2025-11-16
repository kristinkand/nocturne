using System.Text.Json.Serialization;

namespace Nocturne.Core.Models;

/// <summary>
/// Loop notification data model matching the legacy loop.js data structure
/// Maintains 1:1 compatibility with the original Nightscout Loop implementation
/// </summary>
public class LoopNotificationData
{
    /// <summary>
    /// Event type - determines the type of Loop notification to send
    /// Valid values: "Temporary Override Cancel", "Temporary Override", "Remote Carbs Entry", "Remote Bolus Entry"
    /// </summary>
    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Notes/comments associated with the notification
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    /// <summary>
    /// Username or identifier of who entered the data
    /// </summary>
    [JsonPropertyName("enteredBy")]
    public string? EnteredBy { get; set; }

    /// <summary>
    /// Reason for temporary override (used when eventType is "Temporary Override")
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>
    /// Display name for the reason (used when eventType is "Temporary Override")
    /// </summary>
    [JsonPropertyName("reasonDisplay")]
    public string? ReasonDisplay { get; set; }

    /// <summary>
    /// Duration in minutes for temporary override
    /// </summary>
    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    /// <summary>
    /// Remote carbs amount in grams (used when eventType is "Remote Carbs Entry")
    /// </summary>
    [JsonPropertyName("remoteCarbs")]
    public string? RemoteCarbs { get; set; }

    /// <summary>
    /// Carb absorption time in hours (used when eventType is "Remote Carbs Entry")
    /// </summary>
    [JsonPropertyName("remoteAbsorption")]
    public string? RemoteAbsorption { get; set; }

    /// <summary>
    /// Remote bolus amount in units (used when eventType is "Remote Bolus Entry")
    /// </summary>
    [JsonPropertyName("remoteBolus")]
    public string? RemoteBolus { get; set; }

    /// <summary>
    /// One-time password for secure operations
    /// </summary>
    [JsonPropertyName("otp")]
    public string? Otp { get; set; }

    /// <summary>
    /// Timestamp when the entry was created (ISO 8601 format)
    /// </summary>
    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }
}

/// <summary>
/// Loop settings from user profile, matching the legacy loopSettings structure
/// </summary>
public class LoopSettings
{
    /// <summary>
    /// Apple Push Notification device token for the Loop app
    /// </summary>
    [JsonPropertyName("deviceToken")]
    public string? DeviceToken { get; set; }

    /// <summary>
    /// iOS bundle identifier for the Loop app
    /// </summary>
    [JsonPropertyName("bundleIdentifier")]
    public string? BundleIdentifier { get; set; }
}

/// <summary>
/// Loop configuration settings from environment variables
/// Matches the legacy env.extendedSettings.loop structure
/// </summary>
public class LoopConfiguration
{
    /// <summary>
    /// Apple Push Notification Service (APNS) private key in PEM format
    /// </summary>
    public string? ApnsKey { get; set; }

    /// <summary>
    /// APNS Key ID (10-character string)
    /// </summary>
    public string? ApnsKeyId { get; set; }

    /// <summary>
    /// Apple Developer Team ID (10-character string)
    /// </summary>
    public string? DeveloperTeamId { get; set; }

    /// <summary>
    /// APNS environment - "production" or "sandbox"
    /// </summary>
    public string? PushServerEnvironment { get; set; }
}

/// <summary>
/// Loop notification response model
/// </summary>
public class LoopNotificationResponse
{
    /// <summary>
    /// Indicates if the notification was sent successfully
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Response message or error description
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the response was generated
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    /// <summary>
    /// Additional response data
    /// </summary>
    [JsonPropertyName("data")]
    public object? Data { get; set; }
}
