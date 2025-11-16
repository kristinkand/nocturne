using Microsoft.Extensions.DependencyInjection;
using Nocturne.Connectors.MiniMed.Models;
using Nocturne.Connectors.MiniMed.Services;

namespace Nocturne.API.Services.BackgroundServices;

/// <summary>
/// Background service for MiniMed CareLink connector
/// </summary>
public class MiniMedConnectorBackgroundService
    : ConnectorBackgroundService<CareLinkConnectorConfiguration>
{
    public MiniMedConnectorBackgroundService(
        IServiceProvider serviceProvider,
        CareLinkConnectorConfiguration config,
        ILogger<MiniMedConnectorBackgroundService> logger
    )
        : base(serviceProvider, config, logger) { }

    protected override string ConnectorName => "MiniMed CareLink";

    protected override async Task<bool> PerformSyncAsync(CancellationToken cancellationToken)
    {
        using var scope = ServiceProvider.CreateScope();
        var connectorService = scope.ServiceProvider.GetRequiredService<CareLinkConnectorService>();

        return await connectorService.SyncCareLinkDataAsync(Config, cancellationToken);
    }
}
