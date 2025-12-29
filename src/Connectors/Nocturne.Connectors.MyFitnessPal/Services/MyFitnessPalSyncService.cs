using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.Configurations;
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

    private readonly MyFitnessPalConnectorConfiguration _config;

    public MyFitnessPalSyncService(
        ILogger<MyFitnessPalSyncService> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        MyFitnessPalConnectorConfiguration config
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider =
            serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _config = config ?? throw new ArgumentNullException(nameof(config));

        // Get username from bound configuration (populated from CONNECT_MFP_USERNAME env var)
        _username = _config.MyFitnessPalUsername;

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
                "MyFitnessPal username not configured. Set Parameters:Connectors:MyFitnessPal:Username in appsettings.json. Service will not sync data."
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

            // Perform sync using the background overload
            var success = await connectorService.SyncDataAsync(_config, cancellationToken);

            if (success)
            {
                _logger.LogInformation("MyFitnessPal sync completed successfully");
            }
            else
            {
                _logger.LogWarning("MyFitnessPal sync failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during MyFitnessPal sync operation");
            // Don't rethrow - we want the service to keep running and try again next time
        }
    }


}
