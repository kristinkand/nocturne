using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Glooko.Services;

namespace Nocturne.Connectors.Glooko;

/// <summary>
/// Health check for the Glooko connector service
/// </summary>
public class GlookoHealthCheck : IHealthCheck
{
    private readonly GlookoConnectorService _connectorService;
    private readonly ILogger<GlookoHealthCheck> _logger;

    public GlookoHealthCheck(
        GlookoConnectorService connectorService,
        ILogger<GlookoHealthCheck> logger
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
                    "Glooko connector has {FailureCount} consecutive failures",
                    failureCount
                );

                return Task.FromResult(
                    HealthCheckResult.Unhealthy(
                        $"Glooko connector has {failureCount} consecutive failures",
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
                    "Glooko connector is healthy",
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
            _logger.LogError(ex, "Error checking Glooko connector health");

            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    "Error checking Glooko connector health",
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
