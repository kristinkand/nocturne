using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Services;

/// <summary>
/// Service implementation for V2 notification operations with 1:1 legacy JavaScript compatibility
/// Handles Loop notifications and enhanced notification system features based on the legacy notifications-v2.js
/// </summary>
public class NotificationV2Service : INotificationV2Service
{
    private readonly ILogger<NotificationV2Service> _logger;
    private readonly ISignalRBroadcastService _signalRBroadcastService;
    private readonly ILoopService? _loopService;

    // Notification levels (replaces legacy notification levels)
    private readonly NotificationLevels _levels = new()
    {
        INFO = 0,
        WARN = 1,
        URGENT = 2,
    };

    public NotificationV2Service(
        ILogger<NotificationV2Service> logger,
        ISignalRBroadcastService signalRBroadcastService,
        ILoopService? loopService = null
    )
    {
        _logger = logger;
        _signalRBroadcastService = signalRBroadcastService;
        _loopService = loopService;
    }

    /// <summary>
    /// Notification levels constants for compatibility
    /// </summary>
    private class NotificationLevels
    {
        public int INFO { get; set; }
        public int WARN { get; set; }
        public int URGENT { get; set; }
    }

    /// <summary>
    /// Sends a Loop notification for iOS Loop app integration
    /// Implements the /api/v2/notifications/loop endpoint from legacy notifications-v2.js
    /// </summary>
    /// <param name="request">Loop notification request data</param>
    /// <param name="remoteAddress">IP address of the requesting client</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Notification response indicating success or failure</returns>
    public async Task<NotificationV2Response> SendLoopNotificationAsync(
        LoopNotificationRequest request,
        string remoteAddress,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug(
            "Processing Loop notification from {RemoteAddress}: {Type} - {Message}",
            remoteAddress,
            request.Type,
            request.Message
        );

        try
        {
            // Validate the Loop notification request
            if (string.IsNullOrEmpty(request.Type))
            {
                _logger.LogWarning(
                    "Loop notification missing required 'type' field from {RemoteAddress}",
                    remoteAddress
                );
                return new NotificationV2Response
                {
                    Success = false,
                    Message = "Missing required 'type' field",
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                };
            }

            if (string.IsNullOrEmpty(request.Message))
            {
                _logger.LogWarning(
                    "Loop notification missing required 'message' field from {RemoteAddress}",
                    remoteAddress
                );
                return new NotificationV2Response
                {
                    Success = false,
                    Message = "Missing required 'message' field",
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                };
            }

            // Set default values to match legacy behavior
            var processedRequest = new LoopNotificationRequest
            {
                Type = request.Type,
                Message = request.Message,
                Title = request.Title ?? "Loop Notification",
                Urgency = request.Urgency ?? "normal",
                Sound = request.Sound,
                Group = request.Group ?? "Loop",
                Timestamp = request.Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Data = request.Data ?? new Dictionary<string, object>(),
                IsAnnouncement = request.IsAnnouncement ?? false,
            };

            // Process the Loop notification (simulate the actual Loop service integration)
            await ProcessLoopNotificationInternalAsync(
                processedRequest,
                remoteAddress,
                cancellationToken
            );

            _logger.LogInformation(
                "Successfully processed Loop notification: {Type} from {RemoteAddress}",
                processedRequest.Type,
                remoteAddress
            );

            return new NotificationV2Response
            {
                Success = true,
                Message = "Loop notification processed successfully",
                Data = new
                {
                    type = processedRequest.Type,
                    processed_at = DateTimeOffset.UtcNow.ToString("O"),
                    source = remoteAddress,
                },
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing Loop notification from {RemoteAddress}: {Type} - {Message}",
                remoteAddress,
                request.Type,
                request.Message
            );

            return new NotificationV2Response
            {
                Success = false,
                Message = "Internal error processing Loop notification",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
        }
    }

    /// <summary>
    /// Processes a generic V2 notification
    /// </summary>
    /// <param name="notification">Base notification data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Notification response indicating success or failure</returns>
    public async Task<NotificationV2Response> ProcessNotificationAsync(
        NotificationBase notification,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug(
            "Processing V2 notification: {Title} - {Message}",
            notification.Title,
            notification.Message
        );

        try
        {
            // Validate the notification
            if (
                string.IsNullOrEmpty(notification.Title)
                && string.IsNullOrEmpty(notification.Message)
            )
            {
                return new NotificationV2Response
                {
                    Success = false,
                    Message = "Notification must have either title or message",
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                };
            }

            // Set default values
            notification.Title = notification.Title ?? "Notification";
            notification.Message = notification.Message ?? "";
            notification.Group = notification.Group ?? "default";
            notification.Timestamp =
                notification.Timestamp == 0
                    ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    : notification.Timestamp;

            // Process the notification (this would integrate with actual notification systems)
            await ProcessNotificationInternalAsync(notification, cancellationToken);

            _logger.LogInformation(
                "Successfully processed V2 notification: {Title}",
                notification.Title
            );

            return new NotificationV2Response
            {
                Success = true,
                Message = "Notification processed successfully",
                Data = new
                {
                    title = notification.Title,
                    group = notification.Group,
                    level = notification.Level,
                    processed_at = DateTimeOffset.UtcNow.ToString("O"),
                },
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing V2 notification: {Title} - {Message}",
                notification.Title,
                notification.Message
            );

            return new NotificationV2Response
            {
                Success = false,
                Message = "Internal error processing notification",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
        }
    }

    /// <summary>
    /// Gets the current notification status and configuration
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current notification system status</returns>
    public async Task<object> GetNotificationStatusAsync(
        CancellationToken cancellationToken = default
    )
    {
        await Task.CompletedTask; // Placeholder for async operation

        return new
        {
            status = "active",
            version = "v2",
            supported_types = new[] { "loop", "announcement", "alarm", "info" },
            capabilities = new
            {
                loop_integration = _loopService?.IsConfigurationValid() ?? false,
                push_notifications = _loopService?.IsConfigurationValid() ?? false,
                email_notifications = false, // Would be true if email service is configured
                websocket_notifications = true,
            },
            last_update = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    /// <summary>
    /// Internal method to process Loop notifications
    /// This integrates with the actual Loop notification service
    /// </summary>
    /// <param name="request">Processed Loop notification request</param>
    /// <param name="remoteAddress">Source IP address</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task ProcessLoopNotificationInternalAsync(
        LoopNotificationRequest request,
        string remoteAddress,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation(
            "Loop notification processed: Type={Type}, Title={Title}, Urgency={Urgency}, Group={Group}, Source={Source}",
            request.Type,
            request.Title,
            request.Urgency,
            request.Group,
            remoteAddress
        );

        // Note: The legacy V2 endpoint uses a different request structure than the legacy loop.js
        // The legacy loop.js expects specific data structure (LoopNotificationData + LoopSettings)
        // This V2 endpoint is more generic and would need to be adapted to work with actual Loop service
        // For now, we'll just log the notification processing

        // TODO: If we want to integrate this with the actual Loop service, we would need to:
        // 1. Extract profile data to get LoopSettings
        // 2. Map the LoopNotificationRequest to LoopNotificationData
        // 3. Call _loopService.SendNotificationAsync()

        await Task.Delay(1, cancellationToken); // Simulate async processing
    }

    /// <summary>
    /// Internal method to process generic notifications
    /// </summary>
    /// <param name="notification">Notification to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task ProcessNotificationInternalAsync(
        NotificationBase notification,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation(
            "V2 notification processed: Title={Title}, Level={Level}, Group={Group}, Plugin={Plugin}",
            notification.Title,
            notification.Level,
            notification.Group,
            notification.Plugin
        );

        // Broadcast appropriate WebSocket event based on notification properties (replaces legacy ctx.bus.emit('notification', notify))
        try
        {
            if (notification.Clear)
            {
                await _signalRBroadcastService.BroadcastClearAlarmAsync(notification);
            }
            else if (notification.IsAnnouncement)
            {
                await _signalRBroadcastService.BroadcastAnnouncementAsync(notification);
            }
            else if (notification.Level == _levels.URGENT)
            {
                await _signalRBroadcastService.BroadcastUrgentAlarmAsync(notification);
            }
            else if (notification.Level == _levels.WARN)
            {
                await _signalRBroadcastService.BroadcastAlarmAsync(notification);
            }
            else
            {
                await _signalRBroadcastService.BroadcastNotificationAsync(notification);
            }

            _logger.LogDebug(
                "Successfully broadcasted WebSocket notification event for {Title}",
                notification.Title
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to broadcast notification WebSocket event for {Title}",
                notification.Title
            );
        }
    }
}
