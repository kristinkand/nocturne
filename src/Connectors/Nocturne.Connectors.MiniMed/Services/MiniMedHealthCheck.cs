using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.MiniMed.Services;

namespace Nocturne.Connectors.MiniMed;

/// <summary>
/// Health check for the MiniMed CareLink connector service
/// </summary>
public class MiniMedHealthCheck : IHealthCheck
{
    private readonly CareLinkConnectorService _connectorService;
    private readonly ILogger<MiniMedHealthCheck> _logger;

    public MiniMedHealthCheck(
        CareLinkConnectorService connectorService,
        ILogger<MiniMedHealthCheck> logger
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
                    "MiniMed connector has {FailureCount} consecutive failures",
                    failureCount
                );

                return Task.FromResult(
                    HealthCheckResult.Unhealthy(
                        $"MiniMed connector has {failureCount} consecutive failures",
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
                    "MiniMed connector is healthy",
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
            _logger.LogError(ex, "Error checking MiniMed connector health");

            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    "Error checking MiniMed connector health",
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
