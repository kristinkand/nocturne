using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Dexcom.Services;

namespace Nocturne.Connectors.Dexcom;

/// <summary>
/// Health check for the Dexcom connector service
/// </summary>
public class DexcomHealthCheck : IHealthCheck
{
    private readonly DexcomConnectorService _connectorService;
    private readonly ILogger<DexcomHealthCheck> _logger;

    public DexcomHealthCheck(
        DexcomConnectorService connectorService,
        ILogger<DexcomHealthCheck> logger
    )
    {
        _connectorService = connectorService;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // Check if the connector service is healthy based on recent failures
            if (!_connectorService.IsHealthy)
            {
                var failureCount = _connectorService.FailedRequestCount;
                _logger.LogWarning(
                    "Dexcom connector has {FailureCount} consecutive failures",
                    failureCount
                );

                return Task.FromResult(
                    HealthCheckResult.Unhealthy(
                        $"Dexcom connector has {failureCount} consecutive failures",
                        data: new Dictionary<string, object>
                        {
                            ["FailedRequestCount"] = failureCount,
                            ["ServiceName"] = _connectorService.ServiceName,
                        }
                    )
                );
            }

            // Optionally test connectivity (commented out to avoid excessive API calls during health checks)
            // var canAuthenticate = await _connectorService.AuthenticateAsync();
            // if (!canAuthenticate)
            // {
            //     return HealthCheckResult.Degraded("Dexcom authentication failed");
            // }

            return Task.FromResult(
                HealthCheckResult.Healthy(
                    "Dexcom connector is healthy",
                    data: new Dictionary<string, object>
                    {
                        ["ServiceName"] = _connectorService.ServiceName,
                        ["FailedRequestCount"] = _connectorService.FailedRequestCount,
                    }
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Dexcom connector health");

            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    "Error checking Dexcom connector health",
                    ex,
                    data: new Dictionary<string, object>
                    {
                        ["ServiceName"] = _connectorService.ServiceName,
                        ["Error"] = ex.Message,
                    }
                )
            );
        }
    }
}
