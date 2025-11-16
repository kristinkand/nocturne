using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Repositories;
using Nocturne.API.Configuration;
using Nocturne.API.Services.Compatibility;

namespace Nocturne.API.Extensions;

/// <summary>
/// Extension methods for configuring compatibility proxy services
/// </summary>
public static class CompatibilityProxyServiceExtensions
{
    /// <summary>
    /// Add compatibility proxy services to the service collection
    /// </summary>
    public static IServiceCollection AddCompatibilityProxyServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // Add the DbContext
        services.AddDbContext<NocturneDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("nocturne")));

        // Configure compatibility proxy options
        services.Configure<CompatibilityProxyConfiguration>(
            configuration.GetSection(CompatibilityProxyConfiguration.ConfigurationSection)
        );

        // Register core services
        services.AddScoped<IRequestCloningService, RequestCloningService>();
        services.AddScoped<IRequestForwardingService, RequestForwardingService>();

        services.AddScoped<ICorrelationService, CorrelationService>();
        services.AddScoped<IResponseComparisonService, ResponseComparisonService>();
        services.AddScoped<IResponseCacheService, ResponseCacheService>();

        services.AddScoped<DiscrepancyAnalysisRepository>();
        services.AddScoped<IDiscrepancyPersistenceService, DiscrepancyPersistenceService>();
        services.AddScoped<ICompatibilityReportService, CompatibilityReportService>();

        services.AddHostedService<DiscrepancyMaintenanceService>();

        // Add memory cache for response caching
        services.AddMemoryCache();

        // Configure HTTP clients for target systems
        services.AddHttpClients(configuration);

        // Add health checks for compatibility proxy-specific functionality
        services.AddHealthChecks().AddCheck<CompatibilityProxyHealthCheck>("compatibility-proxy");

        return services;
    }

    private static IServiceCollection AddHttpClients(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var interceptorConfig =
            configuration
                .GetSection(CompatibilityProxyConfiguration.ConfigurationSection)
                .Get<CompatibilityProxyConfiguration>() ?? new CompatibilityProxyConfiguration();

        // Nightscout HTTP client
        services
            .AddHttpClient(
                "NightscoutClient",
                client =>
                {
                    if (!string.IsNullOrEmpty(interceptorConfig.NightscoutUrl))
                    {
                        client.BaseAddress = new Uri(interceptorConfig.NightscoutUrl);
                    }
                    client.Timeout = TimeSpan.FromSeconds(interceptorConfig.TimeoutSeconds);
                    client.DefaultRequestHeaders.Add(
                        "User-Agent",
                        "Nocturne-CompatibilityProxy/1.0"
                    );
                }
            )
            .AddStandardResilienceHandler();

        // Nocturne HTTP client
        services
            .AddHttpClient(
                "NocturneClient",
                client =>
                {
                    if (!string.IsNullOrEmpty(interceptorConfig.NocturneUrl))
                    {
                        client.BaseAddress = new Uri(interceptorConfig.NocturneUrl);
                    }
                    client.Timeout = TimeSpan.FromSeconds(interceptorConfig.TimeoutSeconds);
                    client.DefaultRequestHeaders.Add(
                        "User-Agent",
                        "Nocturne-CompatibilityProxy/1.0"
                    );
                }
            )
            .AddStandardResilienceHandler();

        return services;
    }
}

/// <summary>
/// Health check for the compatibility proxy service
/// </summary>
public class CompatibilityProxyHealthCheck : IHealthCheck
{
    private readonly IOptions<CompatibilityProxyConfiguration> _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CompatibilityProxyHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of the CompatibilityProxyHealthCheck class
    /// </summary>
    /// <param name="configuration">CompatibilityProxy configuration settings</param>
    /// <param name="httpClientFactory">Factory for creating HTTP clients</param>
    /// <param name="logger">Logger instance for this health check</param>
    public CompatibilityProxyHealthCheck(
        IOptions<CompatibilityProxyConfiguration> configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<CompatibilityProxyHealthCheck> logger
    )
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Perform the health check for the compatibility proxy service
    /// </summary>
    /// <param name="context">Health check context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check result</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var config = _configuration.Value;
            var healthData = new Dictionary<string, object>();

            // Check configuration - if no URLs are configured, the service is simply not in use
            if (
                string.IsNullOrEmpty(config.NightscoutUrl)
                && string.IsNullOrEmpty(config.NocturneUrl)
            )
            {
                _logger.LogDebug(
                    "Compatibility proxy service is not configured - no target URLs set"
                );
                return HealthCheckResult.Healthy("Service not configured (optional)");
            }

            // Check Nightscout connectivity if configured
            if (!string.IsNullOrEmpty(config.NightscoutUrl))
            {
                var nightscoutHealthy = await CheckTargetHealthAsync(
                    "NightscoutClient",
                    config.NightscoutUrl,
                    cancellationToken
                );
                healthData["nightscout"] = nightscoutHealthy ? "healthy" : "unhealthy";
            }

            // Check Nocturne connectivity if configured
            if (!string.IsNullOrEmpty(config.NocturneUrl))
            {
                var nocturneHealthy = await CheckTargetHealthAsync(
                    "NocturneClient",
                    config.NocturneUrl,
                    cancellationToken
                );
                healthData["nocturne"] = nocturneHealthy ? "healthy" : "unhealthy";
            }

            // Add Phase 2 feature status
            healthData["responseComparison"] = config.Comparison.EnableDeepComparison
                ? "enabled"
                : "disabled";
            healthData["responseCaching"] = config.EnableResponseCaching ? "enabled" : "disabled";
            healthData["correlationTracking"] = config.EnableCorrelationTracking
                ? "enabled"
                : "disabled";
            healthData["abTesting"] =
                config.ABTestingPercentage > 0
                    ? $"enabled ({config.ABTestingPercentage}%)"
                    : "disabled";

            healthData["configuration"] = "valid";
            return HealthCheckResult.Healthy("Compatibility proxy service is healthy", healthData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return HealthCheckResult.Unhealthy("Health check failed", ex);
        }
    }

    private async Task<bool> CheckTargetHealthAsync(
        string clientName,
        string targetUrl,
        CancellationToken cancellationToken
    )
    {
        try
        {
            using var client = _httpClientFactory.CreateClient(clientName);
            client.Timeout = TimeSpan.FromSeconds(5); // Short timeout for health checks

            // Try to connect to the target URL with a HEAD request
            using var response = await client.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, "/"),
                cancellationToken
            );

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Target {ClientName} health check failed", clientName);
            return false;
        }
    }
}
