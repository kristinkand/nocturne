using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.FreeStyle.Services;

namespace Nocturne.Connectors.FreeStyle;

/// <summary>
/// Health check for the FreeStyle LibreLinkUp connector service
/// </summary>
public class FreeStyleHealthCheck : IHealthCheck
{
    private readonly LibreConnectorService _connectorService;
    private readonly ILogger<FreeStyleHealthCheck> _logger;

    public FreeStyleHealthCheck(
        LibreConnectorService connectorService,
        ILogger<FreeStyleHealthCheck> logger
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
                    "FreeStyle connector has {FailureCount} consecutive failures",
                    failureCount
                );

                return Task.FromResult(
                    HealthCheckResult.Unhealthy(
                        $"FreeStyle connector has {failureCount} consecutive failures",
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
                    "FreeStyle connector is healthy",
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
            _logger.LogError(ex, "Error checking FreeStyle connector health");

            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    "Error checking FreeStyle connector health",
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
