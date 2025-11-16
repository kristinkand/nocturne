using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Nightscout.Services;

namespace Nocturne.Connectors.Nightscout;

/// <summary>
/// Health check for the Nightscout-to-Nightscout connector service
/// </summary>
public class NightscoutHealthCheck : IHealthCheck
{
    private readonly NightscoutConnectorService _connectorService;
    private readonly ILogger<NightscoutHealthCheck> _logger;

    public NightscoutHealthCheck(
        NightscoutConnectorService connectorService,
        ILogger<NightscoutHealthCheck> logger
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
                    "Nightscout connector has {FailureCount} consecutive failures",
                    failureCount
                );

                return Task.FromResult(
                    HealthCheckResult.Unhealthy(
                        $"Nightscout connector has {failureCount} consecutive failures",
                        data: new Dictionary<string, object>
                        {
                            ["FailedRequestCount"] = failureCount,
                            ["ServiceName"] = _connectorService.ServiceName,
                        }
                    )
                );
            }

            return Task.FromResult(
                HealthCheckResult.Healthy(
                    "Nightscout connector is healthy",
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
            _logger.LogError(ex, "Error checking Nightscout connector health");

            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    "Error checking Nightscout connector health",
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
