namespace Nocturne.Core.Models;

/// <summary>
/// Data transfer object for discrepancy analysis results
/// </summary>
public class DiscrepancyAnalysisDto
{
    public Guid Id { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset AnalysisTimestamp { get; set; }
    public string RequestMethod { get; set; } = string.Empty;
    public string RequestPath { get; set; } = string.Empty;
    public int OverallMatch { get; set; }
    public bool StatusCodeMatch { get; set; }
    public bool BodyMatch { get; set; }
    public int? NightscoutStatusCode { get; set; }
    public int? NocturneStatusCode { get; set; }
    public long? NightscoutResponseTimeMs { get; set; }
    public long? NocturneResponseTimeMs { get; set; }
    public long TotalProcessingTimeMs { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? SelectedResponseTarget { get; set; }
    public string? SelectionReason { get; set; }
    public int CriticalDiscrepancyCount { get; set; }
    public int MajorDiscrepancyCount { get; set; }
    public int MinorDiscrepancyCount { get; set; }
    public bool NightscoutMissing { get; set; }
    public bool NocturneMissing { get; set; }
    public string? ErrorMessage { get; set; }
    public List<DiscrepancyDetailDto> Discrepancies { get; set; } = new();
}

/// <summary>
/// Data transfer object for detailed discrepancy information
/// </summary>
public class DiscrepancyDetailDto
{
    public Guid Id { get; set; }
    public int DiscrepancyType { get; set; }
    public int Severity { get; set; }
    public string Field { get; set; } = string.Empty;
    public string NightscoutValue { get; set; } = string.Empty;
    public string NocturneValue { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
}

/// <summary>
/// Real-time compatibility status for dashboard
/// </summary>
public class CompatibilityStatus
{
    public double OverallScore { get; set; }
    public int TotalRequests { get; set; }
    public string HealthStatus { get; set; } = string.Empty;
    public DateTimeOffset LastUpdated { get; set; }
    public int CriticalIssues { get; set; }
    public int MajorIssues { get; set; }
    public int MinorIssues { get; set; }
}

/// <summary>
/// Migration readiness assessment report
/// </summary>
public class MigrationReadinessReport
{
    public DateTimeOffset GeneratedAt { get; set; }
    public DateTimeOffset? PeriodStart { get; set; }
    public DateTimeOffset? PeriodEnd { get; set; }
    public double OverallCompatibilityScore { get; set; }
    public int TotalRequestsAnalyzed { get; set; }
    public int CriticalIssues { get; set; }
    public int MajorIssues { get; set; }
    public int MinorIssues { get; set; }
    public MigrationReadinessLevel ReadinessLevel { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public List<string> RiskAreas { get; set; } = new();
    public double? PerformanceRatio { get; set; }
    public string? PerformanceWarning { get; set; }
    public string? PerformanceNote { get; set; }
}

/// <summary>
/// Migration readiness levels
/// </summary>
public enum MigrationReadinessLevel
{
    NotReady = 0,
    NeedsWork = 1,
    NearReady = 2,
    Ready = 3,
}

/// <summary>
/// Type of response match
/// </summary>
public enum ResponseMatchType
{
    /// <summary>
    /// Responses match perfectly
    /// </summary>
    Perfect,

    /// <summary>
    /// Minor differences found
    /// </summary>
    MinorDifferences,

    /// <summary>
    /// Major differences found
    /// </summary>
    MajorDifferences,

    /// <summary>
    /// Critical differences found
    /// </summary>
    CriticalDifferences,

    /// <summary>
    /// Nightscout response is missing
    /// </summary>
    NightscoutMissing,

    /// <summary>
    /// Nocturne response is missing
    /// </summary>
    NocturneMissing,

    /// <summary>
    /// Both responses are missing
    /// </summary>
    BothMissing,

    /// <summary>
    /// Error occurred during comparison
    /// </summary>
    ComparisonError,
}

/// <summary>
/// Type of discrepancy found during comparison
/// </summary>
public enum DiscrepancyType
{
    /// <summary>
    /// HTTP status code differs
    /// </summary>
    StatusCode,

    /// <summary>
    /// Response header differs
    /// </summary>
    Header,

    /// <summary>
    /// Content type differs
    /// </summary>
    ContentType,

    /// <summary>
    /// Response body differs
    /// </summary>
    Body,

    /// <summary>
    /// JSON structure differs
    /// </summary>
    JsonStructure,

    /// <summary>
    /// String value differs
    /// </summary>
    StringValue,

    /// <summary>
    /// Numeric value differs
    /// </summary>
    NumericValue,

    /// <summary>
    /// Timestamp differs
    /// </summary>
    Timestamp,

    /// <summary>
    /// Array length differs
    /// </summary>
    ArrayLength,

    /// <summary>
    /// Performance metrics differ significantly
    /// </summary>
    Performance,
}

/// <summary>
/// Severity level of a discrepancy
/// </summary>
public enum DiscrepancySeverity
{
    /// <summary>
    /// Minor difference that likely doesn't affect functionality
    /// </summary>
    Minor,

    /// <summary>
    /// Major difference that might affect functionality
    /// </summary>
    Major,

    /// <summary>
    /// Critical difference that likely affects functionality
    /// </summary>
    Critical,
}
