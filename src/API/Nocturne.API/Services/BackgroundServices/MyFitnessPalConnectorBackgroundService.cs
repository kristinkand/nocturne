using Microsoft.Extensions.DependencyInjection;
using Nocturne.Connectors.Configurations;
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
        // Get username from bound configuration (populated from CONNECT_MFP_USERNAME env var or appsettings)
        _username = config.MyFitnessPalUsername;
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

        // Get the date range for sync - sync last 7 days by default
        var syncDays = _configuration.GetValue<int>(
            "Connectors:MyFitnessPal:SyncDays",
            Config.SyncDays
        );
        var fromDate = DateTime.Today.AddDays(-syncDays);

        return await connectorService.SyncDataAsync(Config, cancellationToken, fromDate);
    }
}
