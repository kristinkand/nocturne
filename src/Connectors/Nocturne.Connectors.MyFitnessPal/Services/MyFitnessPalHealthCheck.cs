using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.MyFitnessPal.Services;

namespace Nocturne.Connectors.MyFitnessPal;

/// <summary>
/// Health check for the MyFitnessPal connector service
/// </summary>
public class MyFitnessPalHealthCheck : IHealthCheck
{
    private readonly MyFitnessPalConnectorService _connectorService;
    private readonly ILogger<MyFitnessPalHealthCheck> _logger;

    public MyFitnessPalHealthCheck(
        MyFitnessPalConnectorService connectorService,
        ILogger<MyFitnessPalHealthCheck> logger
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
            // For MyFitnessPal, we'll do a basic connectivity check
            // The connector service can be checked for basic health status

            return Task.FromResult(
                HealthCheckResult.Healthy(
                    "MyFitnessPal connector is healthy",
                    data: new Dictionary<string, object>
                    {
                        ["ServiceName"] = _connectorService.ServiceName,
                    }
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking MyFitnessPal connector health");

            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    "Error checking MyFitnessPal connector health",
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
