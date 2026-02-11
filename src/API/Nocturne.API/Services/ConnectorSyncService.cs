using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.Dexcom.Configurations;
using Nocturne.Connectors.Dexcom.Services;
using Nocturne.Connectors.FreeStyle.Configurations;
using Nocturne.Connectors.FreeStyle.Services;
using Nocturne.Connectors.Glooko.Configurations;
using Nocturne.Connectors.Glooko.Services;
using Nocturne.Connectors.MyFitnessPal.Configurations;
using Nocturne.Connectors.MyFitnessPal.Services;
using Nocturne.Connectors.MyLife.Configurations;
using Nocturne.Connectors.MyLife.Services;
using Nocturne.Connectors.Tidepool.Configurations;
using Nocturne.Connectors.Tidepool.Services;

namespace Nocturne.API.Services;

/// <summary>
/// Dispatches manual sync requests to the correct connector service by name.
/// </summary>
public interface IConnectorSyncService
{
    Task<SyncResult> TriggerSyncAsync(
        string connectorId,
        SyncRequest request,
        CancellationToken ct
    );
}

/// <summary>
/// Resolves the concrete connector service by name and executes a sync.
/// Follows the same scope/resolve pattern as the connector background services.
/// </summary>
public class ConnectorSyncService : IConnectorSyncService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ConnectorSyncService> _logger;

    public ConnectorSyncService(
        IServiceProvider serviceProvider,
        ILogger<ConnectorSyncService> logger
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<SyncResult> TriggerSyncAsync(
        string connectorId,
        SyncRequest request,
        CancellationToken ct
    )
    {
        _logger.LogInformation("Manual sync triggered for connector {ConnectorId}", connectorId);

        try
        {
            var result = connectorId.ToLowerInvariant() switch
            {
                "dexcom" => await ExecuteSyncAsync<
                    DexcomConnectorService,
                    DexcomConnectorConfiguration
                >(request, ct),
                "tidepool" => await ExecuteSyncAsync<
                    TidepoolConnectorService,
                    TidepoolConnectorConfiguration
                >(request, ct),
                "librelinkup" => await ExecuteSyncAsync<
                    LibreConnectorService,
                    LibreLinkUpConnectorConfiguration
                >(request, ct),
                "glooko" => await ExecuteSyncAsync<
                    GlookoConnectorService,
                    GlookoConnectorConfiguration
                >(request, ct),
                "mylife" => await ExecuteSyncAsync<
                    MyLifeConnectorService,
                    MyLifeConnectorConfiguration
                >(request, ct),
                "myfitnesspal" => await ExecuteSyncAsync<
                    MyFitnessPalConnectorService,
                    MyFitnessPalConnectorConfiguration
                >(request, ct),
                _ => new SyncResult
                {
                    Success = false,
                    Message = $"Unknown connector: {connectorId}",
                },
            };

            _logger.LogInformation(
                "Manual sync for {ConnectorId} completed: Success={Success}, Message={Message}",
                connectorId,
                result.Success,
                result.Message
            );

            return result;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No service for type"))
        {
            _logger.LogWarning(
                "Connector {ConnectorId} is not registered (likely disabled)",
                connectorId
            );
            return new SyncResult
            {
                Success = false,
                Message = $"Connector '{connectorId}' is not configured or is disabled",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error during manual sync for connector {ConnectorId}",
                connectorId
            );
            return new SyncResult { Success = false, Message = $"Sync failed: {ex.Message}" };
        }
    }

    private async Task<SyncResult> ExecuteSyncAsync<TService, TConfig>(
        SyncRequest request,
        CancellationToken ct
    )
        where TService : class, IConnectorService<TConfig>
        where TConfig : class, IConnectorConfiguration
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<TService>();
        var config = scope.ServiceProvider.GetRequiredService<TConfig>();
        return await service.SyncDataAsync(request, config, ct);
    }
}
