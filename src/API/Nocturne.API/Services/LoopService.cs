using System.Globalization;
using dotAPNS;
using Microsoft.Extensions.Options;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Services;

/// <summary>
/// Loop service implementation with 1:1 legacy JavaScript compatibility
/// Handles Apple Push Notification Service (APNS) integration for iOS Loop app notifications
/// Implements the functionality from legacy loop.js with full backwards compatibility
/// </summary>
public class LoopService : ILoopService, IDisposable
{
    private readonly ILogger<LoopService> _logger;
    private readonly LoopConfiguration _configuration;
    private readonly IApnsClient? _apnsClient;
    private readonly HttpClient _httpClient;
    private bool disposedValue;

    public LoopService(
        ILogger<LoopService> logger,
        IOptions<LoopConfiguration> configuration,
        IHttpClientFactory httpClientFactory
    )
    {
        _logger = logger;
        _configuration = configuration.Value;
        _httpClient = httpClientFactory.CreateClient("dotAPNS");

        // Initialize APNS client if configuration is valid
        if (IsConfigurationValid())
        {
            try
            {
                var apnsOptions = new ApnsJwtOptions
                {
                    KeyId = _configuration.ApnsKeyId!,
                    TeamId = _configuration.DeveloperTeamId!,
                    CertContent = _configuration.ApnsKey!,
                    BundleId = string.Empty, // Will be set per notification from loopSettings
                };

                _apnsClient = ApnsClient.CreateUsingJwt(_httpClient, apnsOptions);

                _logger.LogInformation(
                    "Loop service initialized successfully with {Environment} APNS environment",
                    _configuration.PushServerEnvironment ?? "development"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize APNS client for Loop notifications");
                _apnsClient = null;
            }
        }
        else
        {
            _logger.LogWarning(
                "Loop service configuration is invalid, APNS client not initialized"
            );
        }
    }

    /// <summary>
    /// Sends a Loop notification via Apple Push Notification Service (APNS)
    /// Implements the legacy loop.sendNotification() functionality with 1:1 compatibility
    /// </summary>
    public async Task<LoopNotificationResponse> SendNotificationAsync(
        LoopNotificationData data,
        LoopSettings? loopSettings,
        string remoteAddress,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug(
            "Processing Loop notification: {EventType} from {RemoteAddress}",
            data.EventType,
            remoteAddress
        );

        try
        {
            // Validate configuration (matches legacy validation)
            var configValidation = ValidateConfiguration();
            if (!configValidation.IsValid)
            {
                return CreateErrorResponse(
                    configValidation.ErrorMessage ?? "Configuration validation failed"
                );
            }

            // Validate loop settings (matches legacy validation)
            var settingsValidation = ValidateLoopSettings(loopSettings);
            if (!settingsValidation.IsValid)
            {
                return CreateErrorResponse(
                    settingsValidation.ErrorMessage ?? "Settings validation failed"
                );
            }

            // Process notification based on event type (matches legacy logic)
            var notificationResult = await ProcessNotificationByEventType(
                data,
                loopSettings!, // Validation ensures this is not null
                remoteAddress,
                cancellationToken
            );

            return notificationResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error processing Loop notification from {RemoteAddress}: {EventType}",
                remoteAddress,
                data.EventType
            );

            return CreateErrorResponse($"Loop notification failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates Loop configuration settings
    /// Implements the legacy configuration validation checks
    /// </summary>
    public bool IsConfigurationValid()
    {
        return ValidateConfiguration().IsValid;
    }

    /// <summary>
    /// Gets the current Loop configuration status for debugging
    /// </summary>
    public object GetConfigurationStatus()
    {
        var validation = ValidateConfiguration();

        return new
        {
            IsValid = validation.IsValid,
            Message = validation.ErrorMessage ?? "Configuration is valid",
            HasApnsKey = !string.IsNullOrEmpty(_configuration.ApnsKey),
            HasApnsKeyId = !string.IsNullOrEmpty(_configuration.ApnsKeyId),
            HasDeveloperTeamId = !string.IsNullOrEmpty(_configuration.DeveloperTeamId)
                && _configuration.DeveloperTeamId.Length == 10,
            PushServerEnvironment = _configuration.PushServerEnvironment ?? "development",
            ApnsClientInitialized = _apnsClient != null,
        };
    }

    /// <summary>
    /// Validates configuration settings matching legacy validation logic
    /// </summary>
    private (bool IsValid, string? ErrorMessage) ValidateConfiguration()
    {
        if (string.IsNullOrEmpty(_configuration.ApnsKey))
        {
            return (
                false,
                "Loop notification failed: Invalid configuration - LOOP_APNS_KEY not set."
            );
        }

        if (string.IsNullOrEmpty(_configuration.ApnsKeyId))
        {
            return (
                false,
                "Loop notification failed: Invalid configuration - LOOP_APNS_KEY_ID not set."
            );
        }

        if (
            string.IsNullOrEmpty(_configuration.DeveloperTeamId)
            || _configuration.DeveloperTeamId.Length != 10
        )
        {
            return (
                false,
                "Loop notification failed: Invalid configuration - LOOP_DEVELOPER_TEAM_ID not set."
            );
        }

        return (true, null);
    }

    /// <summary>
    /// Validates loop settings from user profile matching legacy validation logic
    /// </summary>
    private (bool IsValid, string? ErrorMessage) ValidateLoopSettings(LoopSettings? loopSettings)
    {
        if (loopSettings == null)
        {
            return (false, "Loop notification failed: Loop settings are required.");
        }

        if (string.IsNullOrEmpty(loopSettings.DeviceToken))
        {
            return (false, "Loop notification failed: Could not find deviceToken in loopSettings.");
        }

        if (string.IsNullOrEmpty(loopSettings.BundleIdentifier))
        {
            return (false, "Loop notification failed: Bundle ID is required in loopSettings.");
        }

        return (true, null);
    }

    /// <summary>
    /// Processes notification based on event type, implementing legacy event type handling
    /// </summary>
    private async Task<LoopNotificationResponse> ProcessNotificationByEventType(
        LoopNotificationData data,
        LoopSettings loopSettings, // Validation ensures this is not null when called
        string remoteAddress,
        CancellationToken cancellationToken
    )
    {
        if (_apnsClient == null)
        {
            return CreateErrorResponse("APNS client not initialized");
        }

        // Build payload and alert message based on event type (matches legacy logic exactly)
        var (payload, alert, isValid, errorMessage) = BuildNotificationPayload(data, remoteAddress);

        if (!isValid)
        {
            return CreateErrorResponse(errorMessage!);
        }

        // Create APNS push notification using the correct dotAPNS API
        var push = new ApplePush(ApplePushType.Alert)
            .AddAlert(alert)
            .AddToken(loopSettings.DeviceToken!)
            .AddContentAvailable();

        // Add custom payload properties to the root level
        foreach (var kvp in payload)
        {
            push.AddCustomProperty(kvp.Key, kvp.Value);
        }
        try
        {
            // Update bundle ID for this specific push
            var pushOptions = new ApnsJwtOptions
            {
                KeyId = _configuration.ApnsKeyId!,
                TeamId = _configuration.DeveloperTeamId!,
                CertContent = _configuration.ApnsKey!,
                BundleId = loopSettings.BundleIdentifier!,
            };

            // Create client specifically for this bundle ID
            var bundleSpecificClient = ApnsClient.CreateUsingJwt(_httpClient, pushOptions);

            LoopNotificationResponse result;
            try
            {
                // Send notification via APNS
                var response = await bundleSpecificClient.SendAsync(push);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation(
                        "Loop notification sent successfully: {EventType} to {DeviceToken} from {RemoteAddress}",
                        data.EventType,
                        MaskDeviceToken(loopSettings.DeviceToken!),
                        remoteAddress
                    );

                    result = new LoopNotificationResponse
                    {
                        Success = true,
                        Message = "Loop notification sent successfully",
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Data = new
                        {
                            eventType = data.EventType,
                            sentAt = payload["sent-at"],
                            expiration = payload["expiration"],
                        },
                    };
                }
                else
                {
                    var apnsErrorMessage =
                        $"APNs delivery failed: {response.ReasonString ?? "Unknown reason"}";
                    _logger.LogError(
                        "APNs delivery failed for {EventType}: {Reason}",
                        data.EventType,
                        response.ReasonString
                    );

                    result = CreateErrorResponse(apnsErrorMessage);
                }
            }
            finally
            {
                // Dispose the bundle-specific client if it implements IDisposable
                if (bundleSpecificClient is IDisposable disposableClient)
                {
                    disposableClient.Dispose();
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during APNS delivery for {EventType}", data.EventType);
            return CreateErrorResponse($"APNs delivery failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds notification payload and alert message based on event type
    /// Implements the legacy payload building logic exactly
    /// </summary>
    private (
        Dictionary<string, object> payload,
        string alert,
        bool isValid,
        string? errorMessage
    ) BuildNotificationPayload(LoopNotificationData data, string remoteAddress)
    {
        var payload = new Dictionary<string, object>
        {
            ["remote-address"] = remoteAddress,
            ["notes"] = data.Notes ?? string.Empty,
            ["entered-by"] = data.EnteredBy ?? string.Empty,
        };

        string alert;

        // Process based on event type (matches legacy switch logic exactly)
        switch (data.EventType)
        {
            case "Temporary Override Cancel":
                payload["cancel-temporary-override"] = "true";
                alert = "Cancel Temporary Override";
                break;

            case "Temporary Override":
                payload["override-name"] = data.Reason ?? string.Empty;
                if (
                    !string.IsNullOrEmpty(data.Duration)
                    && int.TryParse(data.Duration, out var duration)
                    && duration > 0
                )
                {
                    payload["override-duration-minutes"] = duration;
                }
                alert = $"{data.ReasonDisplay ?? data.Reason} Temporary Override";
                break;

            case "Remote Carbs Entry":
                if (
                    !float.TryParse(
                        data.RemoteCarbs,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var carbsEntry
                    )
                    || carbsEntry <= 0
                )
                {
                    return (
                        new Dictionary<string, object>(),
                        string.Empty,
                        false,
                        $"Loop remote carbs failed. Incorrect carbs entry: {data.RemoteCarbs}"
                    );
                }

                payload["carbs-entry"] = carbsEntry;
                payload["absorption-time"] = 3.0; // Default 3 hours

                if (
                    !string.IsNullOrEmpty(data.RemoteAbsorption)
                    && float.TryParse(
                        data.RemoteAbsorption,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var absorption
                    )
                    && absorption > 0
                )
                {
                    payload["absorption-time"] = absorption;
                }

                if (!string.IsNullOrEmpty(data.Otp))
                {
                    payload["otp"] = data.Otp;
                }

                if (!string.IsNullOrEmpty(data.CreatedAt))
                {
                    payload["start-time"] = data.CreatedAt;
                }

                alert =
                    $"Remote Carbs Entry: {carbsEntry} grams\nAbsorption Time: {payload["absorption-time"]} hours";
                break;

            case "Remote Bolus Entry":
                if (
                    !float.TryParse(
                        data.RemoteBolus,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var bolusEntry
                    )
                    || bolusEntry <= 0
                )
                {
                    return (
                        new Dictionary<string, object>(),
                        string.Empty,
                        false,
                        $"Loop remote bolus failed. Incorrect bolus entry: {data.RemoteBolus}"
                    );
                }

                payload["bolus-entry"] = bolusEntry;

                if (!string.IsNullOrEmpty(data.Otp))
                {
                    payload["otp"] = data.Otp;
                }

                alert = $"Remote Bolus Entry: {bolusEntry} U";
                break;

            default:
                return (
                    new Dictionary<string, object>(),
                    string.Empty,
                    false,
                    $"Loop notification failed: Unhandled event type: {data.EventType}"
                );
        }

        // Add notes and entered by to alert if present (matches legacy logic)
        if (!string.IsNullOrEmpty(data.Notes))
        {
            alert += $" - {data.Notes}";
        }

        if (!string.IsNullOrEmpty(data.EnteredBy))
        {
            alert += $" - {data.EnteredBy}";
        }

        // Add timestamps (matches legacy logic exactly)
        var now = DateTimeOffset.UtcNow;
        payload["sent-at"] = now.ToString("O"); // ISO 8601 format

        var expiration = now.AddMinutes(5); // Expire after 5 minutes
        payload["expiration"] = expiration.ToString("O");

        return (payload, alert, true, null);
    }

    /// <summary>
    /// Creates a standardized error response
    /// </summary>
    private LoopNotificationResponse CreateErrorResponse(string message)
    {
        return new LoopNotificationResponse
        {
            Success = false,
            Message = message,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
    }

    /// <summary>
    /// Masks device token for logging (shows first 8 and last 4 characters)
    /// </summary>
    private string MaskDeviceToken(string deviceToken)
    {
        if (string.IsNullOrEmpty(deviceToken) || deviceToken.Length < 12)
            return "***";

        return $"{deviceToken[..8]}...{deviceToken[^4..]}";
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // Dispose of the APNS client if it implements IDisposable
                if (_apnsClient is IDisposable disposableApnsClient)
                {
                    disposableApnsClient.Dispose();
                }
            }

            disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~LoopService()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
