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

                    // Use the new SyncNightscoutDataAsync method which uploads to Nocturne API
                    var success = await connectorService.SyncNightscoutDataAsync(
                        _config,
                        stoppingToken
                    );

                    if (success)
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
                // Enforce a minimum interval of 1 minute to prevent tight loops
                var intervalMinutes = Math.Max(1, _config.SyncIntervalMinutes);
                var syncInterval = TimeSpan.FromMinutes(intervalMinutes);
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
