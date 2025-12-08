using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nocturne.Connectors.Core.Interfaces;

namespace Nocturne.Connectors.Core.Health
{
    public static class HealthCheckBuilderExtensions
    {
        public static IHealthChecksBuilder AddConnectorHealthCheck(this IHealthChecksBuilder builder, string connectorName)
        {
            // Use factory method to properly inject the connector name
            return builder.AddCheck(
                connectorName,
                sp => new ConnectorHealthCheck(
                    sp.GetRequiredService<IConnectorMetricsTracker>(),
                    connectorName),
                tags: new[] { "connector", "metrics" }
            );
        }
    }
}
