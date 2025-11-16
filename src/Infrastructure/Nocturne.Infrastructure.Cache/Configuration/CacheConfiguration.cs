namespace Nocturne.Infrastructure.Cache.Configuration;

/// <summary>
/// Configuration for in-memory caching
/// </summary>
public class CacheConfiguration
{
    public const string SectionName = "Cache";

    /// <summary>
    /// Key prefix for cache entries
    /// </summary>
    public string KeyPrefix { get; set; } = "nocturne";

    /// <summary>
    /// Default cache expiration in seconds
    /// </summary>
    public int DefaultExpirationSeconds { get; set; } = 300;

    /// <summary>
    /// Whether to enable background cache refresh
    /// </summary>
    public bool EnableBackgroundCacheRefresh { get; set; } = false;

    /// <summary>
    /// Whether to enable calculation cache compression
    /// </summary>
    public bool EnableCalculationCacheCompression { get; set; } = false;
}
