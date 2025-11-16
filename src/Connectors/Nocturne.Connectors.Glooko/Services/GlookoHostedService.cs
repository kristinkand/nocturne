using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.Glooko.Models;
using Nocturne.Connectors.Glooko.Services;

namespace Nocturne.Connectors.Glooko;

/// <summary>
/// Hosted service that runs the Glooko connector in the background
/// </summary>
public class GlookoHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GlookoHostedService> _logger;
    private readonly GlookoConnectorConfiguration _config;

    public GlookoHostedService(
        IServiceProvider serviceProvider,
        ILogger<GlookoHostedService> logger,
        IOptions<GlookoConnectorConfiguration> config
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Glooko Hosted Service started");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var connectorService =
                        scope.ServiceProvider.GetRequiredService<GlookoConnectorService>();

                    _logger.LogDebug("Starting Glooko data sync cycle");

                    var success = await connectorService.SyncGlookoHealthDataAsync(
                        _config,
                        stoppingToken
                    );

                    if (success)
                    {
                        _logger.LogInformation("Glooko data sync completed successfully");
                    }
                    else
                    {
                        _logger.LogWarning("Glooko data sync failed");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during Glooko data sync cycle");
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
            _logger.LogInformation("Glooko Hosted Service cancellation requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Glooko Hosted Service");
            throw;
        }
        finally
        {
            _logger.LogInformation("Glooko Hosted Service stopped");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Glooko Hosted Service is stopping...");
        await base.StopAsync(cancellationToken);
    }
}
