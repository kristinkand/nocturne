using System.Threading;
using System.Threading.Tasks;

namespace Nocturne.Connectors.MyFitnessPal.Services;

/// <summary>
/// Interface for MyFitnessPal sync operations
/// </summary>
public interface IMyFitnessPalManualSyncService
{
    /// <summary>
    /// Triggers a manual sync operation
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if sync was successful</returns>
    Task<bool> TriggerManualSyncAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the sync service status
    /// </summary>
    /// <returns>True if the service is properly configured</returns>
    bool IsConfigured();
}
