using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.MyFitnessPal.Models;
using Nocturne.Connectors.MyFitnessPal.Services;
using Nocturne.Core.Constants;

namespace Nocturne.Connectors.MyFitnessPal.Services;

/// <summary>
/// Background service for periodic MyFitnessPal data synchronization
/// </summary>
public class MyFitnessPalSyncService : BackgroundService
{
    private readonly ILogger<MyFitnessPalSyncService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _syncInterval;
    private readonly string? _username;

    public MyFitnessPalSyncService(
        ILogger<MyFitnessPalSyncService> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider =
            serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        // Get configuration values
        _username = _configuration[ServiceNames.ConfigKeys.MyFitnessPalUsername];

        // Default sync interval is 1 hour, configurable via MyFitnessPal:SyncIntervalMinutes
        var syncIntervalMinutes = _configuration.GetValue<int>(
            "MyFitnessPal:SyncIntervalMinutes",
            60
        );
        _syncInterval = TimeSpan.FromMinutes(syncIntervalMinutes);

        _logger.LogInformation(
            "MyFitnessPal sync service initialized with username: {Username}, sync interval: {SyncInterval}",
            _username ?? "[NOT CONFIGURED]",
            _syncInterval
        );
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_username))
        {
            _logger.LogWarning(
                "MyFitnessPal username not configured. Set MyFitnessPal:Username in configuration. Service will not sync data."
            );
            return;
        }

        _logger.LogInformation(
            "MyFitnessPal sync service starting for user: {Username}",
            _username
        );

        try
        {
            // Create a scope to get scoped services for authentication
            using var scope = _serviceProvider.CreateScope();
            var connectorService =
                scope.ServiceProvider.GetRequiredService<MyFitnessPalConnectorService>();

            // Authenticate on startup
            var authSuccess = await connectorService.AuthenticateAsync();
            if (!authSuccess)
            {
                _logger.LogError(
                    "Failed to authenticate with MyFitnessPal. Service will not continue."
                );
                return;
            }

            // Initial sync
            await PerformSyncAsync(stoppingToken);

            // Periodic sync
            using var timer = new PeriodicTimer(_syncInterval);
            while (
                !stoppingToken.IsCancellationRequested
                && await timer.WaitForNextTickAsync(stoppingToken)
            )
            {
                await PerformSyncAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "MyFitnessPal sync service stopping due to cancellation request"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in MyFitnessPal sync service");
            throw;
        }
    }

    /// <summary>
    /// Performs a single sync operation
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task PerformSyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Starting MyFitnessPal data sync for user: {Username}",
                _username
            );

            // Create a scope to get scoped services
            using var scope = _serviceProvider.CreateScope();
            var connectorService =
                scope.ServiceProvider.GetRequiredService<MyFitnessPalConnectorService>();

            // Get the date range for sync - sync last 7 days by default
            var syncDays = _configuration.GetValue<int>("MyFitnessPal:SyncDays", 7);
            var fromDate = DateTime.Today.AddDays(-syncDays);
            var toDate = DateTime.Today;

            // Fetch diary data from MyFitnessPal
            var diaryResponse = await connectorService.FetchDiaryAsync(
                _username!,
                fromDate,
                toDate
            );

            if (diaryResponse?.Any() == true)
            {
                // Convert to Nightscout foods
                var foods = connectorService.ConvertToNightscoutFoods(diaryResponse);
                if (foods.Any())
                {
                    // Upload to Nightscout (this would need to be implemented)
                    var config = new MyFitnessPalConnectorConfiguration
                    {
                        NightscoutUrl =
                            _configuration[ServiceNames.ConfigKeys.MyFitnessPalNightscoutUrl]
                            ?? _configuration[ServiceNames.ConfigKeys.NightscoutUrl]
                            ?? _configuration[ServiceNames.ConfigKeys.NightscoutTargetUrl]
                            ?? string.Empty,
                    };

                    var uploadSuccess = await connectorService.UploadFoodToNightscoutAsync(
                        foods,
                        config
                    );

                    if (uploadSuccess)
                    {
                        _logger.LogInformation(
                            "Successfully synced {FoodCount} food entries from MyFitnessPal",
                            foods.Count()
                        );
                    }
                    else
                    {
                        _logger.LogWarning("Failed to upload food entries to Nightscout");
                    }
                }
                else
                {
                    _logger.LogInformation(
                        "No food entries found in MyFitnessPal diary for sync period"
                    );
                }
            }
            else
            {
                _logger.LogInformation("No diary data found in MyFitnessPal for sync period");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during MyFitnessPal sync operation");
            // Don't rethrow - we want the service to keep running and try again next time
        }
    }

    /// <summary>
    /// Triggers a manual sync (can be called from an API endpoint)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if sync was successful</returns>
    public async Task<bool> TriggerManualSyncAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_username))
        {
            _logger.LogWarning("Cannot trigger manual sync - MyFitnessPal username not configured");
            return false;
        }

        try
        {
            _logger.LogInformation(
                "Manual MyFitnessPal sync triggered for user: {Username}",
                _username
            );
            await PerformSyncAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual MyFitnessPal sync");
            return false;
        }
    }
}
