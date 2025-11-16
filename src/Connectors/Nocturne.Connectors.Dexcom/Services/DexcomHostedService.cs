using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.Dexcom.Models;
using Nocturne.Connectors.Dexcom.Services;

namespace Nocturne.Connectors.Dexcom;

/// <summary>
/// Hosted service that runs the Dexcom connector in the background
/// </summary>
public class DexcomHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DexcomHostedService> _logger;
    private readonly DexcomConnectorConfiguration _config;

    public DexcomHostedService(
        IServiceProvider serviceProvider,
        ILogger<DexcomHostedService> logger,
        DexcomConnectorConfiguration config
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Dexcom Hosted Service started");

        try
        {
            var syncInterval = TimeSpan.FromMinutes(_config.SyncIntervalMinutes);
            _logger.LogInformation(
                "Dexcom sync interval set to {SyncInterval} minutes",
                syncInterval.TotalMinutes
            );

            using var timer = new PeriodicTimer(syncInterval);

            do
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var connectorService =
                        scope.ServiceProvider.GetRequiredService<DexcomConnectorService>();

                    _logger.LogDebug("Starting Dexcom data sync cycle");

                    var success = await connectorService.SyncDexcomDataAsync(
                        _config,
                        stoppingToken
                    );

                    if (success)
                    {
                        _logger.LogInformation("Dexcom data sync completed successfully");
                    }
                    else
                    {
                        _logger.LogWarning("Dexcom data sync failed");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during Dexcom data sync cycle");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
            _logger.LogInformation("Dexcom Hosted Service cancellation requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Dexcom Hosted Service");
            throw;
        }
        finally
        {
            _logger.LogInformation("Dexcom Hosted Service stopped");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dexcom Hosted Service is stopping...");
        await base.StopAsync(cancellationToken);
    }
}
