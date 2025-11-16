using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nocturne.Infrastructure.Cache.Services;

namespace Nocturne.Infrastructure.Cache.Extensions;

/// <summary>
/// Extension methods for Phase 3 calculation caching service registration
/// </summary>
public static class Phase3CacheServiceExtensions
{
    /// <summary>
    /// Adds Phase 3 calculation caching services to the DI container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration root</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddPhase3CalculationCache(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // Register configuration
        services.Configure<CalculationCacheConfiguration>(options =>
            configuration.GetSection("CalculationCache").Bind(options)
        );

        // Register cache invalidation service
        services.AddScoped<ICacheInvalidationService, CacheInvalidationService>();

        // Register cached calculation services
        services.AddScoped<ICachedIobService, CachedIobService>();
        services.AddScoped<ICachedProfileService, CachedProfileService>();

        return services;
    }

    /// <summary>
    /// Adds Phase 3 calculation caching services with custom configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureOptions">Configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddPhase3CalculationCache(
        this IServiceCollection services,
        Action<CalculationCacheConfiguration> configureOptions
    )
    {
        services.Configure(configureOptions);

        // Register cache invalidation service
        services.AddScoped<ICacheInvalidationService, CacheInvalidationService>();

        // Register cached calculation services
        services.AddScoped<ICachedIobService, CachedIobService>();
        services.AddScoped<ICachedProfileService, CachedProfileService>();

        return services;
    }

    /// <summary>
    /// Adds Phase 3 calculation caching with default configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddPhase3CalculationCacheWithDefaults(
        this IServiceCollection services
    )
    {
        return services.AddPhase3CalculationCache(options =>
        {
            // Use default values from CalculationCacheConfiguration
        });
    }
}
