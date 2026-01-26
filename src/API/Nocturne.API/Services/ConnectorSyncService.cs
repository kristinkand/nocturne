using System.Net.Http.Json;
using Nocturne.Connectors.Core.Services;

namespace Nocturne.API.Services;

/// <summary>
/// Service for triggering manual data synchronization via connector sidecar services.
/// Determines connector availability by querying the connector's health endpoint directly.
/// </summary>
public class ConnectorSyncService : IConnectorSyncService
{
    /// <summary>
    /// Named HTTP client for connector sync operations.
    /// This client has generous timeout settings to allow for long-running sync operations.
    /// </summary>
    public const string HttpClientName = "ConnectorSync";

    private readonly ILogger<ConnectorSyncService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public ConnectorSyncService(
        ILogger<ConnectorSyncService> logger,
        IHttpClientFactory httpClientFactory
    )
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public bool HasEnabledConnectors()
    {
        // This is a synchronous check - we'll assume connectors exist if metadata exists
        // The actual availability is checked when triggering sync
        return ConnectorMetadataService.GetAll().Any();
    }

    /// <inheritdoc />
    public bool IsConnectorConfigured(string connectorId)
    {
        if (string.IsNullOrEmpty(connectorId))
        {
            return false;
        }

        // Check if connector metadata exists
        var connector = ConnectorMetadataService
            .GetAll()
            .FirstOrDefault(c =>
                c.ConnectorName.Equals(connectorId, StringComparison.OrdinalIgnoreCase)
                || c.ServiceName.Equals(connectorId, StringComparison.OrdinalIgnoreCase)
            );

        return connector != null;
    }

    /// <summary>
    /// Checks if a connector is reachable and enabled by querying its health endpoint.
    /// </summary>
    private async Task<(bool IsAvailable, string? ErrorMessage)> CheckConnectorAvailabilityAsync(
        string serviceName,
        string displayName,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var healthUrl = $"http://{serviceName}/health";

            _logger.LogDebug("Checking connector availability at {Url}", healthUrl);

            var response = await client.GetAsync(healthUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return (false, $"Connector '{displayName}' health check failed with status {response.StatusCode}");
            }

            // Connector is reachable and healthy - it's available for sync
            _logger.LogDebug("Connector {DisplayName} is available", displayName);
            return (true, null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to reach connector {DisplayName} at {ServiceName}", displayName, serviceName);
            return (false, $"Connector '{displayName}' is not reachable: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error checking connector {DisplayName} availability", displayName);
            return (false, $"Error checking connector '{displayName}': {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Nocturne.Connectors.Core.Models.SyncResult> TriggerConnectorSyncAsync(
        string connectorId,
        Nocturne.Connectors.Core.Models.SyncRequest request,
        CancellationToken cancellationToken = default
    )
    {
        // Find connector metadata
        var connector = ConnectorMetadataService
            .GetAll()
            .FirstOrDefault(c =>
                c.ConnectorName.Equals(connectorId, StringComparison.OrdinalIgnoreCase)
                || c.ServiceName.Equals(connectorId, StringComparison.OrdinalIgnoreCase)
            );

        if (connector == null)
        {
            return new Nocturne.Connectors.Core.Models.SyncResult
            {
                Success = false,
                Message = $"Connector '{connectorId}' not found",
            };
        }

        // Check if connector is actually reachable by querying its health endpoint
        var (isAvailable, errorMessage) = await CheckConnectorAvailabilityAsync(
            connector.ServiceName,
            connector.DisplayName,
            cancellationToken
        );

        if (!isAvailable)
        {
            return new Nocturne.Connectors.Core.Models.SyncResult
            {
                Success = false,
                Message = errorMessage ?? $"Connector '{connector.DisplayName}' is not available",
            };
        }

        return await SyncConnectorAsync(
            connector.DisplayName,
            connector.ServiceName,
            request,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<
        List<Nocturne.Connectors.Core.Models.SyncDataType>
    > GetConnectorCapabilitiesAsync(
        string connectorId,
        CancellationToken cancellationToken = default
    )
    {
        var connector = ConnectorMetadataService
            .GetAll()
            .FirstOrDefault(c =>
                c.ConnectorName.Equals(connectorId, StringComparison.OrdinalIgnoreCase)
                || c.ServiceName.Equals(connectorId, StringComparison.OrdinalIgnoreCase)
            );

        if (connector == null || string.IsNullOrEmpty(connector.ServiceName))
        {
            return new List<Nocturne.Connectors.Core.Models.SyncDataType>();
        }

        try
        {
            var url = $"http://{connector.ServiceName}/capabilities";
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var response = await client.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var capabilities = await response.Content.ReadFromJsonAsync<
                    List<Nocturne.Connectors.Core.Models.SyncDataType>
                >(cancellationToken: cancellationToken);
                return capabilities ?? new List<Nocturne.Connectors.Core.Models.SyncDataType>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching capabilities for {ConnectorId}", connectorId);
        }

        return new List<Nocturne.Connectors.Core.Models.SyncDataType>();
    }

    /// <summary>
    /// Syncs a single connector by calling the sidecar service
    /// </summary>
    private async Task<Nocturne.Connectors.Core.Models.SyncResult> SyncConnectorAsync(
        string displayName,
        string serviceName,
        Nocturne.Connectors.Core.Models.SyncRequest request,
        CancellationToken cancellationToken
    )
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            var url = $"http://{serviceName}/sync";

            _logger.LogInformation(
                "Triggering sidecar sync for {ConnectorName} at {Url} with request {@Request}",
                displayName,
                url,
                request
            );

            // Use the named HTTP client with connector-specific timeout settings
            var client = _httpClientFactory.CreateClient(HttpClientName);

            // Send SyncRequest as JSON
            var response = await client.PostAsJsonAsync(
                url,
                request,
                cancellationToken: cancellationToken
            );

            if (response.IsSuccessStatusCode)
            {
                var syncResult =
                    await response.Content.ReadFromJsonAsync<Nocturne.Connectors.Core.Models.SyncResult>(
                        cancellationToken: cancellationToken
                    );

                _logger.LogInformation(
                    "Sidecar sync successful for {ConnectorName}. Items: {@ItemsSynced}",
                    displayName,
                    syncResult?.ItemsSynced
                );

                return syncResult
                    ?? new Nocturne.Connectors.Core.Models.SyncResult
                    {
                        Success = true,
                        StartTime = startTime,
                        EndTime = DateTimeOffset.UtcNow,
                    };
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Sidecar sync failed for {ConnectorName}: {StatusCode} - {Error}",
                    displayName,
                    response.StatusCode,
                    error
                );

                return new Nocturne.Connectors.Core.Models.SyncResult
                {
                    Success = false,
                    Message = $"Sidecar returned {response.StatusCode}: {error}",
                    StartTime = startTime,
                    EndTime = DateTimeOffset.UtcNow,
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error triggering sidecar sync for {ConnectorName}: {Message}",
                displayName,
                ex.Message
            );

            return new Nocturne.Connectors.Core.Models.SyncResult
            {
                Success = false,
                Message = ex.Message,
                StartTime = startTime,
                EndTime = DateTimeOffset.UtcNow,
                Errors = new List<string> { ex.Message },
            };
        }
    }
}
