using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;

namespace Nocturne.Aspire.Hosting;

/// <summary>
/// Extension methods for configuring mTLS certificates for internal Nocturne services.
/// </summary>
public static class InternalCertificateExtensions
{
    /// <summary>
    /// Adds internal mTLS certificate generation that runs before resources start.
    /// This generates a CA certificate, server certificate for the API, and client
    /// certificates for each connector.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="connectorNames">Names of connectors that need client certificates.</param>
    /// <returns>The builder for chaining.</returns>
    public static IDistributedApplicationBuilder AddInternalCertificates(
        this IDistributedApplicationBuilder builder,
        params string[] connectorNames)
    {
        builder.Eventing.Subscribe<BeforeStartEvent>((evt, ct) =>
        {
            InternalCertificateGenerator.EnsureCertificatesExist(connectorNames);
            return Task.CompletedTask;
        });

        return builder;
    }

    /// <summary>
    /// Configures a resource (typically the API) to use the server mTLS certificate.
    /// Sets environment variables for the certificate paths.
    /// </summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="resource">The resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<T> WithInternalServerCertificate<T>(
        this IResourceBuilder<T> resource) where T : IResourceWithEnvironment
    {
        return resource
            .WithEnvironment("INTERNAL_CA_CERT_PATH", InternalCertificateGenerator.CaCertificatePath)
            .WithEnvironment("INTERNAL_SERVER_CERT_PATH", InternalCertificateGenerator.ServerCertificatePath)
            .WithEnvironment("INTERNAL_MTLS_ENABLED", "true");
    }

    /// <summary>
    /// Configures a connector resource to use its client mTLS certificate.
    /// Sets environment variables for the certificate paths.
    /// </summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="resource">The resource builder.</param>
    /// <param name="connectorName">The connector name (must match the name used in AddInternalCertificates).</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<T> WithInternalClientCertificate<T>(
        this IResourceBuilder<T> resource,
        string connectorName) where T : IResourceWithEnvironment
    {
        return resource
            .WithEnvironment("INTERNAL_CA_CERT_PATH", InternalCertificateGenerator.CaCertificatePath)
            .WithEnvironment("INTERNAL_CLIENT_CERT_PATH", InternalCertificateGenerator.GetConnectorCertificatePath(connectorName))
            .WithEnvironment("INTERNAL_CONNECTOR_NAME", connectorName)
            .WithEnvironment("INTERNAL_MTLS_ENABLED", "true");
    }

    /// <summary>
    /// Configures a connector resource to use its client mTLS certificate,
    /// inferring the connector name from the resource name.
    /// </summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="resource">The resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<T> WithInternalClientCertificate<T>(
        this IResourceBuilder<T> resource) where T : IResourceWithEnvironment
    {
        // Extract connector name from resource name (e.g., "nocturne-connector-dexcom" -> "dexcom")
        var resourceName = resource.Resource.Name;
        var connectorName = ExtractConnectorName(resourceName);

        return resource.WithInternalClientCertificate(connectorName);
    }

    /// <summary>
    /// Extracts the connector name from a resource name.
    /// </summary>
    private static string ExtractConnectorName(string resourceName)
    {
        // Handle patterns like "nocturne-connector-dexcom" or "connector-dexcom" or just "dexcom"
        const string fullPrefix = "nocturne-connector-";
        const string shortPrefix = "connector-";

        if (resourceName.StartsWith(fullPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return resourceName.Substring(fullPrefix.Length);
        }

        if (resourceName.StartsWith(shortPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return resourceName.Substring(shortPrefix.Length);
        }

        return resourceName;
    }
}
