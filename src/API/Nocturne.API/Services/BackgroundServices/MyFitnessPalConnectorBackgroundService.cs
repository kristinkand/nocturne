using Microsoft.Extensions.DependencyInjection;
using Nocturne.Connectors.MyFitnessPal.Models;
using Nocturne.Connectors.MyFitnessPal.Services;
using Nocturne.Core.Constants;

namespace Nocturne.API.Services.BackgroundServices;

/// <summary>
/// Background service for MyFitnessPal connector
/// </summary>
public class MyFitnessPalConnectorBackgroundService
    : ConnectorBackgroundService<MyFitnessPalConnectorConfiguration>
{
    private readonly IConfiguration _configuration;
    private readonly string? _username;

    public MyFitnessPalConnectorBackgroundService(
        IServiceProvider serviceProvider,
        MyFitnessPalConnectorConfiguration config,
        IConfiguration configuration,
        ILogger<MyFitnessPalConnectorBackgroundService> logger
    )
        : base(serviceProvider, config, logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _username =
            config.MyFitnessPalUsername
            ?? _configuration[ServiceNames.ConfigKeys.MyFitnessPalUsername];
    }

    protected override string ConnectorName => "MyFitnessPal";

    protected override async Task<bool> PerformSyncAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_username))
        {
            Logger.LogWarning(
                "MyFitnessPal username not configured. Service will not sync data."
            );
            return false;
        }

        using var scope = ServiceProvider.CreateScope();
        var connectorService =
            scope.ServiceProvider.GetRequiredService<MyFitnessPalConnectorService>();

        try
        {
            // Get the date range for sync - sync last 7 days by default
            var syncDays = _configuration.GetValue<int>(
                "Connectors:MyFitnessPal:SyncDays",
                Config.SyncDays
            );
            var fromDate = DateTime.Today.AddDays(-syncDays);
            var toDate = DateTime.Today;

            // Fetch diary data from MyFitnessPal
            var diaryResponse = await connectorService.FetchDiaryAsync(
                _username!,
                fromDate,
                toDate
            );

            if (diaryResponse?.Any() != true)
            {
                Logger.LogInformation("No diary data found in MyFitnessPal for sync period");
                return true;
            }

            // Convert to Nightscout foods
            var foods = connectorService.ConvertToNightscoutFoods(diaryResponse);
            if (!foods.Any())
            {
                Logger.LogInformation(
                    "No food entries found in MyFitnessPal diary for sync period"
                );
                return true;
            }

            // Upload to Nightscout
            var uploadSuccess = await connectorService.UploadFoodToNightscoutAsync(foods, Config);

            if (uploadSuccess)
            {
                Logger.LogInformation(
                    "Successfully synced {FoodCount} food entries from MyFitnessPal",
                    foods.Count()
                );
            }
            else
            {
                Logger.LogWarning("Failed to upload food entries to Nightscout");
            }

            return uploadSuccess;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error syncing MyFitnessPal data");
            return false;
        }
    }
}
