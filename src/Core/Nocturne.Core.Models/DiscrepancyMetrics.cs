namespace Nocturne.Core.Models;

/// <summary>
/// Overall compatibility metrics for dashboard
/// </summary>
public class CompatibilityMetrics
{
    public int TotalRequests { get; set; }
    public int PerfectMatches { get; set; }
    public int MinorDifferences { get; set; }
    public int MajorDifferences { get; set; }
    public int CriticalDifferences { get; set; }
    public double CompatibilityScore { get; set; }
    public double AverageNightscoutResponseTime { get; set; }
    public double AverageNocturneResponseTime { get; set; }
}

/// <summary>
/// Per-endpoint compatibility metrics
/// </summary>
public class EndpointMetrics
{
    public string Endpoint { get; set; } = string.Empty;
    public int TotalRequests { get; set; }
    public int PerfectMatches { get; set; }
    public int MinorDifferences { get; set; }
    public int MajorDifferences { get; set; }
    public int CriticalDifferences { get; set; }
    public double CompatibilityScore { get; set; }
    public double AverageNightscoutResponseTime { get; set; }
    public double AverageNocturneResponseTime { get; set; }
}
