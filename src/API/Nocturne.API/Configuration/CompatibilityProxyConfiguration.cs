namespace Nocturne.API.Configuration;

/// <summary>
/// Configuration for the compatibility proxy service target endpoints
/// </summary>
public class CompatibilityProxyConfiguration
{
    /// <summary>
    /// Configuration section name for compatibility proxy settings
    /// </summary>
    public const string ConfigurationSection = "CompatibilityProxy";

    /// <summary>
    /// Nightscout target URL
    /// </summary>
    public string NightscoutUrl { get; set; } = string.Empty;

    /// <summary>
    /// Nocturne target URL
    /// </summary>
    public string NocturneUrl { get; set; } = string.Empty;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Number of retry attempts
    /// </summary>
    public int RetryAttempts { get; set; } = 3;

    /// <summary>
    /// Default response selection strategy
    /// </summary>
    public ResponseSelectionStrategy DefaultStrategy { get; set; } =
        ResponseSelectionStrategy.Nightscout;

    /// <summary>
    /// Enable detailed request/response logging
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Response comparison settings
    /// </summary>
    public ResponseComparisonSettings Comparison { get; set; } = new();

    /// <summary>
    /// Per-endpoint timeout configurations
    /// </summary>
    public Dictionary<string, int> EndpointTimeouts { get; set; } = new();

    /// <summary>
    /// Circuit breaker settings
    /// </summary>
    public CircuitBreakerSettings CircuitBreaker { get; set; } = new();

    /// <summary>
    /// Enable request correlation tracking
    /// </summary>
    public bool EnableCorrelationTracking { get; set; } = true;

    /// <summary>
    /// Enable response caching
    /// </summary>
    public bool EnableResponseCaching { get; set; } = false;

    /// <summary>
    /// Response cache TTL in seconds
    /// </summary>
    public int ResponseCacheTtlSeconds { get; set; } = 300; // 5 minutes

    /// <summary>
    /// Enable request deduplication
    /// </summary>
    public bool EnableRequestDeduplication { get; set; } = false;

    /// <summary>
    /// A/B testing percentage (0-100)
    /// </summary>
    public int ABTestingPercentage { get; set; } = 0;

    /// <summary>
    /// Maximum response size for comparison (in bytes)
    /// </summary>
    public long MaxResponseSizeForComparison { get; set; } = 10 * 1024 * 1024; // 10MB

    /// <summary>
    /// Fields to exclude from sensitive data logging
    /// </summary>
    public List<string> SensitiveFields { get; set; } =
        new() { "api_secret", "token", "password", "key" };
}

/// <summary>
/// Response comparison configuration
/// </summary>
public class ResponseComparisonSettings
{
    /// <summary>
    /// Fields to exclude from comparison globally
    /// </summary>
    public List<string> ExcludeFields { get; set; } = new() { "timestamp", "date", "dateString", "_id", "id", "sysTime", "mills", "created_at", "updated_at" };

    /// <summary>
    /// Per-route field exclusions (route pattern -> list of fields to exclude)
    /// </summary>
    public Dictionary<string, List<string>> RouteExcludeFields { get; set; } = new()
    {
        // Example: "/api/v1/entries" excludes additional fields specific to entries
        { "/api/v1/entries", new() { "sgv", "trend" } },
        { "/api/v1/treatments", new() { "insulin", "carbs" } },
    };

    /// <summary>
    /// Allow superset responses (Nocturne can have extra fields that Nightscout doesn't)
    /// </summary>
    public bool AllowSupersetResponses { get; set; } = true;

    /// <summary>
    /// Tolerance for timestamp differences in milliseconds
    /// </summary>
    public long TimestampToleranceMs { get; set; } = 5000; // 5 seconds

    /// <summary>
    /// Tolerance for numeric precision differences
    /// </summary>
    public double NumericPrecisionTolerance { get; set; } = 0.001;

    /// <summary>
    /// Whether to normalize field ordering
    /// </summary>
    public bool NormalizeFieldOrdering { get; set; } = true;

    /// <summary>
    /// How to handle array order differences
    /// </summary>
    public ArrayOrderHandling ArrayOrderHandling { get; set; } = ArrayOrderHandling.Strict;

    /// <summary>
    /// Enable deep comparison for nested objects
    /// </summary>
    public bool EnableDeepComparison { get; set; } = true;
}

/// <summary>
/// Circuit breaker configuration
/// </summary>
public class CircuitBreakerSettings
{
    /// <summary>
    /// Number of consecutive failures before opening circuit
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Time to wait before attempting to close circuit (in seconds)
    /// </summary>
    public int RecoveryTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Number of successful requests needed to close circuit
    /// </summary>
    public int SuccessThreshold { get; set; } = 3;
}

/// <summary>
/// Strategy for selecting which response to return to the client
/// </summary>
public enum ResponseSelectionStrategy
{
    /// <summary>
    /// Always return Nightscout response (default)
    /// </summary>
    Nightscout,

    /// <summary>
    /// Always return Nocturne response
    /// </summary>
    Nocturne,

    /// <summary>
    /// Return the fastest response
    /// </summary>
    Fastest,

    /// <summary>
    /// Compare responses and return based on configured criteria
    /// </summary>
    Compare,

    /// <summary>
    /// A/B testing mode - gradually shift traffic
    /// </summary>
    ABTest,
}

/// <summary>
/// How to handle array order differences during comparison
/// </summary>
public enum ArrayOrderHandling
{
    /// <summary>
    /// Arrays must have identical ordering
    /// </summary>
    Strict,

    /// <summary>
    /// Arrays can have different ordering but same elements
    /// </summary>
    Loose,

    /// <summary>
    /// Sort arrays before comparison
    /// </summary>
    Sorted,
}
