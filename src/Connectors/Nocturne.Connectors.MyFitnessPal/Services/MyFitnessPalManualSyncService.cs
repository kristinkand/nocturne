using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.MyFitnessPal.Models;
using Nocturne.Core.Constants;

namespace Nocturne.Connectors.MyFitnessPal.Services;

/// <summary>
/// Service for manual MyFitnessPal sync operations that can be injected into controllers
/// </summary>
public class MyFitnessPalManualSyncService : IMyFitnessPalManualSyncService
{
    private readonly ILogger<MyFitnessPalManualSyncService> _logger;
    private readonly MyFitnessPalConnectorService _connectorService;
    private readonly IConfiguration _configuration;
    private readonly string? _username;

    public MyFitnessPalManualSyncService(
        ILogger<MyFitnessPalManualSyncService> logger,
        MyFitnessPalConnectorService connectorService,
        IConfiguration configuration
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectorService =
            connectorService ?? throw new ArgumentNullException(nameof(connectorService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        _username = _configuration[ServiceNames.ConfigKeys.MyFitnessPalUsername];
    }

    /// <summary>
    /// Triggers a manual sync operation
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

            // Authenticate
            var authSuccess = await _connectorService.AuthenticateAsync();
            if (!authSuccess)
            {
                _logger.LogError("Failed to authenticate with MyFitnessPal during manual sync");
                return false;
            }

            // Get the date range for sync - sync last 7 days by default for manual sync
            var syncDays = _configuration.GetValue<int>("MyFitnessPal:SyncDays", 7);
            var fromDate = DateTime.Today.AddDays(-syncDays);
            var toDate = DateTime.Today;

            // Fetch diary data from MyFitnessPal
            var diaryResponse = await _connectorService.FetchDiaryAsync(
                _username!,
                fromDate,
                toDate
            );

            if (diaryResponse?.Any() == true)
            {
                // Convert to Nightscout foods
                var foods = _connectorService.ConvertToNightscoutFoods(diaryResponse);

                if (foods.Any())
                {
                    // Upload to Nightscout
                    var config = new MyFitnessPalConnectorConfiguration
                    {
                        NightscoutUrl =
                            _configuration[ServiceNames.ConfigKeys.MyFitnessPalNightscoutUrl]
                            ?? _configuration[ServiceNames.ConfigKeys.NightscoutUrl]
                            ?? _configuration[ServiceNames.ConfigKeys.NightscoutTargetUrl]
                            ?? string.Empty,
                    };

                    var uploadSuccess = await _connectorService.UploadFoodToNightscoutAsync(
                        foods,
                        config
                    );

                    if (uploadSuccess)
                    {
                        _logger.LogInformation(
                            "Successfully synced {FoodCount} food entries from MyFitnessPal via manual trigger",
                            foods.Count()
                        );
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Failed to upload food entries to Nightscout during manual sync"
                        );
                        return false;
                    }
                }
                else
                {
                    _logger.LogInformation(
                        "No food entries found in MyFitnessPal diary for manual sync period"
                    );
                    return true; // Not an error - just no data
                }
            }
            else
            {
                _logger.LogInformation(
                    "No diary data found in MyFitnessPal for manual sync period"
                );
                return true; // Not an error - just no data
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual MyFitnessPal sync");
            return false;
        }
    }

    /// <summary>
    /// Gets whether the sync service is properly configured
    /// </summary>
    /// <returns>True if the service is properly configured</returns>
    public bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(_username);
    }
}
