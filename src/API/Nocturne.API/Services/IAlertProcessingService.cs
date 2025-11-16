using Nocturne.Core.Models;

namespace Nocturne.API.Services;

/// <summary>
/// Interface for processing and managing alert events
/// </summary>
public interface IAlertProcessingService
{
    /// <summary>
    /// Processes an alert event by creating history entries and sending notifications
    /// </summary>
    /// <param name="alertEvent">Alert event to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ProcessAlertEvent(AlertEvent alertEvent, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves an active alert by marking it as resolved
    /// </summary>
    /// <param name="alertId">Alert ID to resolve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ResolveAlert(Guid alertId, CancellationToken cancellationToken);

    /// <summary>
    /// Acknowledges an alert and optionally snoozes it for a specified duration
    /// </summary>
    /// <param name="alertId">Alert ID to acknowledge</param>
    /// <param name="snoozeMinutes">Minutes to snooze the alert (0 for no snooze)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AcknowledgeAlert(Guid alertId, int snoozeMinutes, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves all active alerts of a specific type for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="alertType">Type of alerts to resolve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ResolveAlertsForUser(
        string userId,
        AlertType alertType,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Processes escalation for alerts that haven't been acknowledged
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ProcessAlertEscalations(CancellationToken cancellationToken);

    /// <summary>
    /// Cleans up old resolved alerts to maintain database performance
    /// </summary>
    /// <param name="daysToKeep">Number of days of resolved alerts to keep</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CleanupOldAlerts(int daysToKeep = 30, CancellationToken cancellationToken = default);
}
