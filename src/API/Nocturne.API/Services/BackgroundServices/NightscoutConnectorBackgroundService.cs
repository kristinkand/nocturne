using Microsoft.Extensions.DependencyInjection;
using Nocturne.Connectors.Nightscout.Models;
using Nocturne.Connectors.Nightscout.Services;

namespace Nocturne.API.Services.BackgroundServices;

/// <summary>
/// Background service for Nightscout-to-Nightscout connector
/// </summary>
public class NightscoutConnectorBackgroundService
    : ConnectorBackgroundService<NightscoutConnectorConfiguration>
{
    public NightscoutConnectorBackgroundService(
        IServiceProvider serviceProvider,
        NightscoutConnectorConfiguration config,
        ILogger<NightscoutConnectorBackgroundService> logger
    )
        : base(serviceProvider, config, logger) { }

    protected override string ConnectorName => "Nightscout";

    protected override async Task<bool> PerformSyncAsync(CancellationToken cancellationToken)
    {
        using var scope = ServiceProvider.CreateScope();
        var connectorService =
            scope.ServiceProvider.GetRequiredService<NightscoutConnectorService>();

        // Nightscout connector has a different pattern - it fetches and uploads separately
        try
        {
            // Fetch glucose data from source Nightscout
            var glucoseEntries = await connectorService.FetchGlucoseDataAsync();

            // Upload to destination Nightscout
            var glucoseSuccess = await connectorService.UploadToNightscoutAsync(
                glucoseEntries,
                Config
            );

            // Fetch and sync treatments
            var treatments = await connectorService.FetchTreatmentsAsync();
            // Note: Treatment upload would need to be implemented in the service

            // Fetch and sync device status
            var deviceStatuses = await connectorService.FetchDeviceStatusAsync();
            var deviceSuccess = await connectorService.UploadDeviceStatusToNightscoutAsync(
                deviceStatuses,
                Config
            );

            return glucoseSuccess && deviceSuccess;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error syncing Nightscout data");
            return false;
        }
    }
}
