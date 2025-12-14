using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.Connectors.Configurations;

namespace Nocturne.Connectors.Tidepool.Services;

/// <summary>
/// Background service that periodically syncs data from Tidepool
/// </summary>
public class TidepoolHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TidepoolHostedService> _logger;
    private readonly TidepoolConnectorConfiguration _config;

    public TidepoolHostedService(
        IServiceProvider serviceProvider,
        IOptions<TidepoolConnectorConfiguration> config,
        ILogger<TidepoolHostedService> logger
    )
    {
        _serviceProvider = serviceProvider;
        _config = config.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Tidepool connector service starting with sync interval of {Interval} minutes",
            _config.SyncIntervalMinutes
        );

        // Initial delay to allow service to fully start
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var connectorService =
                    scope.ServiceProvider.GetRequiredService<TidepoolConnectorService>();

                _logger.LogInformation("Starting scheduled Tidepool data sync");

                var success = await connectorService.SyncTidepoolDataAsync(
                    _config,
                    stoppingToken
                );

                if (success)
                {
                    _logger.LogInformation("Scheduled Tidepool sync completed successfully");
                }
                else
                {
                    _logger.LogWarning("Scheduled Tidepool sync failed");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Tidepool connector service stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled Tidepool sync");
            }

            // Wait for next sync interval
            await Task.Delay(
                TimeSpan.FromMinutes(_config.SyncIntervalMinutes),
                stoppingToken
            );
        }

        _logger.LogInformation("Tidepool connector service stopped");
    }
}
