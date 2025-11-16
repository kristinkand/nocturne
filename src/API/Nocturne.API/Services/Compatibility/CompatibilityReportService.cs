using System.Text;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Repositories;

namespace Nocturne.API.Services.Compatibility;

/// <summary>
/// Service for generating compatibility reports
/// </summary>
public interface ICompatibilityReportService
{
    /// <summary>
    /// Generate a text-based compatibility report
    /// </summary>
    Task<string> GenerateTextReportAsync(
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Generate a migration readiness assessment
    /// </summary>
    Task<MigrationReadinessReport> GenerateMigrationAssessmentAsync(
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Implementation of compatibility report service
/// </summary>
public class CompatibilityReportService : ICompatibilityReportService
{
    private readonly DiscrepancyAnalysisRepository _repository;
    private readonly ILogger<CompatibilityReportService> _logger;

    /// <summary>
    /// Initializes a new instance of the CompatibilityReportService class
    /// </summary>
    /// <param name="repository">Repository for discrepancy analysis operations</param>
    /// <param name="logger">Logger instance for this service</param>
    public CompatibilityReportService(
        DiscrepancyAnalysisRepository repository,
        ILogger<CompatibilityReportService> logger
    )
    {
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> GenerateTextReportAsync(
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                "Generating compatibility report from {FromDate} to {ToDate}",
                fromDate,
                toDate
            );

            var metrics = await _repository.GetCompatibilityMetricsAsync(
                fromDate,
                toDate,
                cancellationToken
            );
            var endpoints = await _repository.GetEndpointMetricsAsync(
                fromDate,
                toDate,
                cancellationToken
            );

            var report = new StringBuilder();

            // Header
            report.AppendLine("NOCTURNE COMPATIBILITY REPORT");
            report.AppendLine("============================");
            report.AppendLine();
            report.AppendLine($"Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            report.AppendLine(
                $"Period: {fromDate?.ToString("yyyy-MM-dd") ?? "All"} to {toDate?.ToString("yyyy-MM-dd") ?? "Now"}"
            );
            report.AppendLine();

            // Overall Metrics
            report.AppendLine("OVERALL COMPATIBILITY METRICS");
            report.AppendLine("-----------------------------");
            report.AppendLine($"Total Requests: {metrics.TotalRequests:N0}");
            report.AppendLine($"Compatibility Score: {metrics.CompatibilityScore:F1}%");
            report.AppendLine(
                $"Perfect Matches: {metrics.PerfectMatches:N0} ({(metrics.TotalRequests > 0 ? (double)metrics.PerfectMatches / metrics.TotalRequests * 100 : 0):F1}%)"
            );
            report.AppendLine(
                $"Minor Differences: {metrics.MinorDifferences:N0} ({(metrics.TotalRequests > 0 ? (double)metrics.MinorDifferences / metrics.TotalRequests * 100 : 0):F1}%)"
            );
            report.AppendLine(
                $"Major Differences: {metrics.MajorDifferences:N0} ({(metrics.TotalRequests > 0 ? (double)metrics.MajorDifferences / metrics.TotalRequests * 100 : 0):F1}%)"
            );
            report.AppendLine(
                $"Critical Differences: {metrics.CriticalDifferences:N0} ({(metrics.TotalRequests > 0 ? (double)metrics.CriticalDifferences / metrics.TotalRequests * 100 : 0):F1}%)"
            );
            report.AppendLine();

            // Performance Metrics
            report.AppendLine("PERFORMANCE COMPARISON");
            report.AppendLine("---------------------");
            report.AppendLine(
                $"Average Nightscout Response Time: {metrics.AverageNightscoutResponseTime:F0}ms"
            );
            report.AppendLine(
                $"Average Nocturne Response Time: {metrics.AverageNocturneResponseTime:F0}ms"
            );

            if (
                metrics.AverageNightscoutResponseTime > 0
                && metrics.AverageNocturneResponseTime > 0
            )
            {
                var percentageDiff =
                    (
                        (
                            metrics.AverageNocturneResponseTime
                            - metrics.AverageNightscoutResponseTime
                        ) / metrics.AverageNightscoutResponseTime
                    ) * 100;
                report.AppendLine(
                    $"Performance Difference: {percentageDiff:+0.0;-0.0}% ({(percentageDiff > 0 ? "slower" : "faster")} than Nightscout)"
                );
            }
            report.AppendLine();

            // Top Problematic Endpoints
            var problematicEndpoints = endpoints
                .Where(e => e.CriticalDifferences + e.MajorDifferences > 0)
                .OrderByDescending(e => e.CriticalDifferences + e.MajorDifferences)
                .Take(10)
                .ToList();

            if (problematicEndpoints.Any())
            {
                report.AppendLine("TOP PROBLEMATIC ENDPOINTS");
                report.AppendLine("------------------------");
                foreach (var endpoint in problematicEndpoints)
                {
                    report.AppendLine($"{endpoint.Endpoint}:");
                    report.AppendLine($"  Requests: {endpoint.TotalRequests:N0}");
                    report.AppendLine($"  Compatibility: {endpoint.CompatibilityScore:F1}%");
                    report.AppendLine(
                        $"  Issues: {endpoint.CriticalDifferences} critical, {endpoint.MajorDifferences} major, {endpoint.MinorDifferences} minor"
                    );
                    report.AppendLine();
                }
            }

            // Best Performing Endpoints
            var bestEndpoints = endpoints
                .Where(e => e.TotalRequests >= 10) // Only include endpoints with meaningful data
                .OrderByDescending(e => e.CompatibilityScore)
                .Take(5)
                .ToList();

            if (bestEndpoints.Any())
            {
                report.AppendLine("BEST PERFORMING ENDPOINTS");
                report.AppendLine("------------------------");
                foreach (var endpoint in bestEndpoints)
                {
                    report.AppendLine(
                        $"{endpoint.Endpoint}: {endpoint.CompatibilityScore:F1}% ({endpoint.TotalRequests:N0} requests)"
                    );
                }
                report.AppendLine();
            }

            // Recommendations
            report.AppendLine("RECOMMENDATIONS");
            report.AppendLine("---------------");

            if (metrics.CompatibilityScore >= 95)
            {
                report.AppendLine(
                    "✓ Excellent compatibility! Nocturne is ready for production migration."
                );
            }
            else if (metrics.CompatibilityScore >= 85)
            {
                report.AppendLine(
                    "✓ Good compatibility. Review critical and major differences before migration."
                );
            }
            else if (metrics.CompatibilityScore >= 70)
            {
                report.AppendLine(
                    "⚠ Fair compatibility. Address critical issues before considering migration."
                );
            }
            else
            {
                report.AppendLine(
                    "✗ Poor compatibility. Significant work needed before migration is safe."
                );
            }

            if (metrics.CriticalDifferences > 0)
            {
                report.AppendLine(
                    $"• Address {metrics.CriticalDifferences} critical compatibility issues"
                );
            }

            if (problematicEndpoints.Any())
            {
                report.AppendLine(
                    $"• Focus on the {problematicEndpoints.Count} most problematic endpoints listed above"
                );
            }

            if (metrics.AverageNocturneResponseTime > metrics.AverageNightscoutResponseTime * 1.5)
            {
                report.AppendLine(
                    "• Investigate performance regression in Nocturne implementation"
                );
            }

            return report.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating compatibility report");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<MigrationReadinessReport> GenerateMigrationAssessmentAsync(
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var metrics = await _repository.GetCompatibilityMetricsAsync(
                fromDate,
                toDate,
                cancellationToken
            );
            var endpoints = await _repository.GetEndpointMetricsAsync(
                fromDate,
                toDate,
                cancellationToken
            );

            var assessment = new MigrationReadinessReport
            {
                GeneratedAt = DateTimeOffset.UtcNow,
                PeriodStart = fromDate,
                PeriodEnd = toDate,
                OverallCompatibilityScore = metrics.CompatibilityScore,
                TotalRequestsAnalyzed = metrics.TotalRequests,
                CriticalIssues = metrics.CriticalDifferences,
                MajorIssues = metrics.MajorDifferences,
                MinorIssues = metrics.MinorDifferences,
            };

            // Determine readiness level
            if (metrics.CompatibilityScore >= 95 && metrics.CriticalDifferences == 0)
            {
                assessment.ReadinessLevel = MigrationReadinessLevel.Ready;
                assessment.Recommendation =
                    "Nocturne is ready for production migration with excellent compatibility.";
            }
            else if (metrics.CompatibilityScore >= 85 && metrics.CriticalDifferences <= 5)
            {
                assessment.ReadinessLevel = MigrationReadinessLevel.NearReady;
                assessment.Recommendation =
                    "Nocturne has good compatibility. Address remaining critical issues before migration.";
            }
            else if (metrics.CompatibilityScore >= 70)
            {
                assessment.ReadinessLevel = MigrationReadinessLevel.NeedsWork;
                assessment.Recommendation =
                    "Nocturne needs more work before migration is safe. Focus on critical compatibility issues.";
            }
            else
            {
                assessment.ReadinessLevel = MigrationReadinessLevel.NotReady;
                assessment.Recommendation =
                    "Nocturne is not ready for migration. Significant compatibility work is required.";
            }

            // Identify risk areas
            assessment.RiskAreas = endpoints
                .Where(e => e.CriticalDifferences > 0)
                .Select(e => e.Endpoint)
                .ToList();

            // Performance assessment
            if (
                metrics.AverageNightscoutResponseTime > 0
                && metrics.AverageNocturneResponseTime > 0
            )
            {
                var performanceRatio =
                    metrics.AverageNocturneResponseTime / metrics.AverageNightscoutResponseTime;
                assessment.PerformanceRatio = performanceRatio;

                if (performanceRatio > 1.5)
                {
                    assessment.PerformanceWarning =
                        "Nocturne is significantly slower than Nightscout. Performance optimization recommended.";
                }
                else if (performanceRatio < 0.8)
                {
                    assessment.PerformanceNote =
                        "Nocturne shows better performance than Nightscout.";
                }
            }

            return assessment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating migration assessment");
            throw;
        }
    }
}
