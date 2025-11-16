using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.MiniMed.Models;
using Nocturne.Connectors.MiniMed.Services;

namespace Nocturne.Connectors.MiniMed;

/// <summary>
/// Hosted service that runs the MiniMed CareLink connector in the background
/// </summary>
public class MiniMedHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MiniMedHostedService> _logger;
    private readonly CareLinkConnectorConfiguration _config;

    public MiniMedHostedService(
        IServiceProvider serviceProvider,
        ILogger<MiniMedHostedService> logger,
        IOptions<CareLinkConnectorConfiguration> config
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MiniMed Hosted Service started");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var connectorService =
                        scope.ServiceProvider.GetRequiredService<CareLinkConnectorService>();

                    _logger.LogDebug("Starting MiniMed data sync cycle");

                    var success = await connectorService.SyncCareLinkDataAsync(
                        _config,
                        stoppingToken
                    );

                    if (success)
                    {
                        _logger.LogInformation("MiniMed data sync completed successfully");
                    }
                    else
                    {
                        _logger.LogWarning("MiniMed data sync failed");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during MiniMed data sync cycle");
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
            _logger.LogInformation("MiniMed Hosted Service cancellation requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in MiniMed Hosted Service");
            throw;
        }
        finally
        {
            _logger.LogInformation("MiniMed Hosted Service stopped");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MiniMed Hosted Service is stopping...");
        await base.StopAsync(cancellationToken);
    }
}
