using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.FreeStyle.Models;
using Nocturne.Connectors.FreeStyle.Services;

namespace Nocturne.Connectors.FreeStyle;

/// <summary>
/// Hosted service that runs the FreeStyle LibreLinkUp connector in the background
/// </summary>
public class FreeStyleHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FreeStyleHostedService> _logger;
    private readonly LibreLinkUpConnectorConfiguration _config;

    public FreeStyleHostedService(
        IServiceProvider serviceProvider,
        ILogger<FreeStyleHostedService> logger,
        IOptions<LibreLinkUpConnectorConfiguration> config
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FreeStyle Hosted Service started");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var connectorService =
                        scope.ServiceProvider.GetRequiredService<LibreConnectorService>();

                    _logger.LogDebug("Starting FreeStyle data sync cycle");

                    var success = await connectorService.SyncLibreDataAsync(_config, stoppingToken);

                    if (success)
                    {
                        _logger.LogInformation("FreeStyle data sync completed successfully");
                    }
                    else
                    {
                        _logger.LogWarning("FreeStyle data sync failed");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during FreeStyle data sync cycle");
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
            _logger.LogInformation("FreeStyle Hosted Service cancellation requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in FreeStyle Hosted Service");
            throw;
        }
        finally
        {
            _logger.LogInformation("FreeStyle Hosted Service stopped");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("FreeStyle Hosted Service is stopping...");
        await base.StopAsync(cancellationToken);
    }
}
