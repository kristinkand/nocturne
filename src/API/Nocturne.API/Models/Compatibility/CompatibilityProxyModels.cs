namespace Nocturne.API.Models.Compatibility;

/// <summary>
/// Represents a cloned HTTP request for forwarding
/// </summary>
public class ClonedRequest
{
    /// <summary>
    /// Request method (GET, POST, PUT, etc.)
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Request path and query string
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Request headers
    /// </summary>
    public Dictionary<string, string[]> Headers { get; set; } = new();

    /// <summary>
    /// Request body content
    /// </summary>
    public byte[]? Body { get; set; }

    /// <summary>
    /// Content type of the request body
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Query parameters
    /// </summary>
    public Dictionary<string, string[]> QueryParameters { get; set; } = new();
}

/// <summary>
/// Represents a response from a target system
/// </summary>
public class TargetResponse
{
    /// <summary>
    /// Target system name (Nightscout or Nocturne)
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// HTTP status code
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Response headers
    /// </summary>
    public Dictionary<string, string[]> Headers { get; set; } = new();

    /// <summary>
    /// Response body content
    /// </summary>
    public byte[]? Body { get; set; }

    /// <summary>
    /// Content type of the response
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Response time in milliseconds
    /// </summary>
    public long ResponseTimeMs { get; set; }

    /// <summary>
    /// Whether the request was successful
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Error message if request failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Combined response information for comparison
/// </summary>
public class CompatibilityProxyResponse
{
    /// <summary>
    /// Nightscout response
    /// </summary>
    public TargetResponse? NightscoutResponse { get; set; }

    /// <summary>
    /// Nocturne response
    /// </summary>
    public TargetResponse? NocturneResponse { get; set; }

    /// <summary>
    /// Selected response to return to client
    /// </summary>
    public TargetResponse? SelectedResponse { get; set; }

    /// <summary>
    /// Reason for response selection
    /// </summary>
    public string SelectionReason { get; set; } = string.Empty;

    /// <summary>
    /// Total processing time in milliseconds
    /// </summary>
    public long TotalProcessingTimeMs { get; set; }

    /// <summary>
    /// Request correlation ID for tracking
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Detailed comparison result if comparison was performed
    /// </summary>
    public ResponseComparisonResult? ComparisonResult { get; set; }
}

/// <summary>
/// Result of comparing two responses
/// </summary>
public class ResponseComparisonResult
{
    /// <summary>
    /// Request correlation ID
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when comparison was performed
    /// </summary>
    public DateTimeOffset ComparisonTimestamp { get; set; }

    /// <summary>
    /// Overall match assessment
    /// </summary>
    public Nocturne.Core.Models.ResponseMatchType OverallMatch { get; set; }

    /// <summary>
    /// Whether status codes match
    /// </summary>
    public bool StatusCodeMatch { get; set; }

    /// <summary>
    /// Whether response bodies match
    /// </summary>
    public bool BodyMatch { get; set; }

    /// <summary>
    /// List of identified discrepancies
    /// </summary>
    public List<ResponseDiscrepancy> Discrepancies { get; set; } = new();

    /// <summary>
    /// Performance comparison data
    /// </summary>
    public PerformanceComparison? PerformanceComparison { get; set; }

    /// <summary>
    /// Summary of comparison results
    /// </summary>
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Represents a discrepancy between two responses
/// </summary>
public class ResponseDiscrepancy
{
    /// <summary>
    /// Type of discrepancy
    /// </summary>
    public DiscrepancyType Type { get; set; }

    /// <summary>
    /// Severity level of the discrepancy
    /// </summary>
    public DiscrepancySeverity Severity { get; set; }

    /// <summary>
    /// Field or path where discrepancy was found
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Value from Nightscout response
    /// </summary>
    public string NightscoutValue { get; set; } = string.Empty;

    /// <summary>
    /// Value from Nocturne response
    /// </summary>
    public string NocturneValue { get; set; } = string.Empty;

    /// <summary>
    /// Description of the discrepancy
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Performance comparison between responses
/// </summary>
public class PerformanceComparison
{
    /// <summary>
    /// Nightscout response time in milliseconds
    /// </summary>
    public long NightscoutResponseTime { get; set; }

    /// <summary>
    /// Nocturne response time in milliseconds
    /// </summary>
    public long NocturneResponseTime { get; set; }

    /// <summary>
    /// Absolute difference in response times
    /// </summary>
    public long TimeDifference { get; set; }

    /// <summary>
    /// Which system responded faster
    /// </summary>
    public string FasterSystem { get; set; } = string.Empty;
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
