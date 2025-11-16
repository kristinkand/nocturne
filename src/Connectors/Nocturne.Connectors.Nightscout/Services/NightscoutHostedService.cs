using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.Nightscout.Models;
using Nocturne.Connectors.Nightscout.Services;

namespace Nocturne.Connectors.Nightscout;

/// <summary>
/// Hosted service that runs the Nightscout-to-Nightscout connector in the background
/// </summary>
public class NightscoutHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NightscoutHostedService> _logger;
    private readonly NightscoutConnectorConfiguration _config;

    public NightscoutHostedService(
        IServiceProvider serviceProvider,
        ILogger<NightscoutHostedService> logger,
        IOptions<NightscoutConnectorConfiguration> config
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Nightscout Hosted Service started");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var connectorService =
                        scope.ServiceProvider.GetRequiredService<NightscoutConnectorService>();

                    _logger.LogDebug("Starting Nightscout data sync cycle");

                    // Fetch glucose data from source Nightscout
                    var glucoseEntries = await connectorService.FetchGlucoseDataAsync();

                    // Upload to destination Nightscout
                    var glucoseSuccess = await connectorService.UploadToNightscoutAsync(
                        glucoseEntries,
                        _config
                    );

                    // Fetch and sync treatments
                    var treatments = await connectorService.FetchTreatmentsAsync();
                    // Note: Treatment upload would need to be implemented in the service

                    // Fetch and sync device status
                    var deviceStatuses = await connectorService.FetchDeviceStatusAsync();
                    var deviceSuccess = await connectorService.UploadDeviceStatusToNightscoutAsync(
                        deviceStatuses,
                        _config
                    );

                    var overallSuccess = glucoseSuccess && deviceSuccess;

                    if (overallSuccess)
                    {
                        _logger.LogInformation("Nightscout data sync completed successfully");
                    }
                    else
                    {
                        _logger.LogWarning("Nightscout data sync failed");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during Nightscout data sync cycle");
                }

                // Wait for the configured interval before next sync
                var syncInterval = TimeSpan.FromMinutes(_config.SyncIntervalMinutes);
                _logger.LogDebug(
                    "Waiting {SyncInterval} minutes until next sync",
                    syncInterval.TotalMinutes
                );

                try
                {
                    await Task.Delay(syncInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
            _logger.LogInformation("Nightscout Hosted Service cancellation requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Nightscout Hosted Service");
            throw;
        }
        finally
        {
            _logger.LogInformation("Nightscout Hosted Service stopped");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Nightscout Hosted Service is stopping...");
        await base.StopAsync(cancellationToken);
    }
}
