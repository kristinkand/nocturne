namespace Nocturne.API.Configuration;

/// <summary>
/// Configuration options for local analytics collection
/// </summary>
public class AnalyticsConfiguration
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "Analytics";

    /// <summary>
    /// Gets or sets whether local analytics collection is enabled (opt-in)
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to include system health metrics
    /// </summary>
    public bool IncludeSystemMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include usage metrics
    /// </summary>
    public bool IncludeUsageMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include performance metrics
    /// </summary>
    public bool IncludePerformanceMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable verbose logging for analytics operations
    /// </summary>
    public bool VerboseLogging { get; set; } = false;
}
