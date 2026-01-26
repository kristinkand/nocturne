using System.Text.Json;

namespace Nocturne.Connectors.Core.Interfaces;

/// <summary>
/// Event raised when connector configuration changes.
/// </summary>
public class ConfigurationChangedEventArgs : EventArgs
{
    /// <summary>
    /// The connector whose configuration changed.
    /// </summary>
    public string ConnectorName { get; set; } = string.Empty;

    /// <summary>
    /// Type of change: "updated", "deleted", "enabled", "disabled", "secrets_updated".
    /// </summary>
    public string ChangeType { get; set; } = string.Empty;

    /// <summary>
    /// When the change occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Client for connectors to fetch their configuration from the API.
/// Supports both HTTP fetching and SignalR subscriptions for real-time updates.
/// </summary>
public interface IConfigurationClient : IAsyncDisposable
{
    /// <summary>
    /// Gets the runtime configuration for a connector.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type</typeparam>
    /// <param name="connectorName">The connector name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Configuration or null if not found</returns>
    Task<TConfig?> GetConfigurationAsync<TConfig>(string connectorName, CancellationToken ct = default)
        where TConfig : class, new();

    /// <summary>
    /// Gets the raw JSON configuration for a connector.
    /// </summary>
    /// <param name="connectorName">The connector name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>JSON document or null if not found</returns>
    Task<JsonDocument?> GetConfigurationJsonAsync(string connectorName, CancellationToken ct = default);

    /// <summary>
    /// Gets the JSON Schema for a connector's configuration.
    /// </summary>
    /// <param name="connectorName">The connector name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>JSON Schema document</returns>
    Task<JsonDocument> GetSchemaAsync(string connectorName, CancellationToken ct = default);

    /// <summary>
    /// Checks if the connector is currently enabled.
    /// </summary>
    /// <param name="connectorName">The connector name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if enabled, false if disabled or not found</returns>
    Task<bool> IsEnabledAsync(string connectorName, CancellationToken ct = default);

    /// <summary>
    /// Subscribes to configuration changes for a connector via SignalR.
    /// </summary>
    /// <param name="connectorName">The connector name to subscribe to</param>
    /// <param name="ct">Cancellation token</param>
    Task SubscribeToChangesAsync(string connectorName, CancellationToken ct = default);

    /// <summary>
    /// Unsubscribes from configuration changes.
    /// </summary>
    /// <param name="connectorName">The connector name to unsubscribe from</param>
    /// <param name="ct">Cancellation token</param>
    Task UnsubscribeFromChangesAsync(string connectorName, CancellationToken ct = default);

    /// <summary>
    /// Event raised when configuration changes are received via SignalR.
    /// </summary>
    event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    /// <summary>
    /// Whether the client is connected to the SignalR hub.
    /// </summary>
    bool IsConnected { get; }
}
