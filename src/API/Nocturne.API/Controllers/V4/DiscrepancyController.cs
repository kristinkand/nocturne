using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Services.Compatibility;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Repositories;

namespace Nocturne.API.Controllers.V4;

/// <summary>
/// Controller for discrepancy analysis and compatibility dashboard
/// Provides endpoints for monitoring Nightscout/Nocturne compatibility
/// </summary>
[ApiController]
[Route("api/v4/[controller]")]
[Produces("application/json")]
[Tags("V4 Discrepancy")]
public class DiscrepancyController : ControllerBase
{
    private readonly DiscrepancyAnalysisRepository _discrepancyRepository;
    private readonly ICompatibilityReportService _reportService;
    private readonly ILogger<DiscrepancyController> _logger;

    public DiscrepancyController(
        DiscrepancyAnalysisRepository discrepancyRepository,
        ICompatibilityReportService reportService,
        ILogger<DiscrepancyController> logger
    )
    {
        _discrepancyRepository = discrepancyRepository;
        _reportService = reportService;
        _logger = logger;
    }

    /// <summary>
    /// Get overall compatibility metrics for dashboard overview
    /// </summary>
    /// <param name="fromDate">Start date for metrics (optional)</param>
    /// <param name="toDate">End date for metrics (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Compatibility metrics including success rate and response times</returns>
    [HttpGet("metrics")]
    public async Task<ActionResult<CompatibilityMetrics>> GetCompatibilityMetrics(
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                "Retrieving compatibility metrics from {FromDate} to {ToDate}",
                fromDate,
                toDate
            );

            var metrics = await _discrepancyRepository.GetCompatibilityMetricsAsync(
                fromDate,
                toDate,
                cancellationToken
            );

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving compatibility metrics");
            return StatusCode(500, "Error retrieving compatibility metrics");
        }
    }

    /// <summary>
    /// Get per-endpoint compatibility metrics
    /// </summary>
    /// <param name="fromDate">Start date for metrics (optional)</param>
    /// <param name="toDate">End date for metrics (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of endpoint-specific compatibility metrics</returns>
    [HttpGet("endpoints")]
    public async Task<ActionResult<IEnumerable<EndpointMetrics>>> GetEndpointMetrics(
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                "Retrieving endpoint metrics from {FromDate} to {ToDate}",
                fromDate,
                toDate
            );

            var metrics = await _discrepancyRepository.GetEndpointMetricsAsync(
                fromDate,
                toDate,
                cancellationToken
            );

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving endpoint metrics");
            return StatusCode(500, "Error retrieving endpoint metrics");
        }
    }

    /// <summary>
    /// Get detailed discrepancy analyses with filtering and pagination
    /// </summary>
    /// <param name="requestPath">Filter by request path (optional)</param>
    /// <param name="overallMatch">Filter by overall match type (optional)</param>
    /// <param name="fromDate">Start date for filter (optional)</param>
    /// <param name="toDate">End date for filter (optional)</param>
    /// <param name="count">Number of results to return (default: 100, max: 1000)</param>
    /// <param name="skip">Number of results to skip for pagination (default: 0)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of detailed discrepancy analyses</returns>
    [HttpGet("analyses")]
    public async Task<ActionResult<IEnumerable<DiscrepancyAnalysisDto>>> GetDiscrepancyAnalyses(
        [FromQuery] string? requestPath = null,
        [FromQuery] int? overallMatch = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        [FromQuery] int count = 100,
        [FromQuery] int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // Validate parameters
            if (count <= 0)
            {
                return BadRequest("Count must be positive");
            }

            if (skip < 0)
            {
                return BadRequest("Skip must be non-negative");
            }

            _logger.LogDebug(
                "Retrieving discrepancy analyses: path={RequestPath}, match={OverallMatch}, count={Count}, skip={Skip}",
                requestPath,
                overallMatch,
                count,
                skip
            );

            var analyses = await _discrepancyRepository.GetAnalysesAsync(
                requestPath,
                overallMatch,
                fromDate,
                toDate,
                count,
                skip,
                cancellationToken
            );

            // Convert to DTOs to avoid exposing internal entities
            var analysisResults = analyses.Select(a => new DiscrepancyAnalysisDto
            {
                Id = a.Id,
                CorrelationId = a.CorrelationId,
                AnalysisTimestamp = a.AnalysisTimestamp,
                RequestMethod = a.RequestMethod,
                RequestPath = a.RequestPath,
                OverallMatch = (int)a.OverallMatch,
                StatusCodeMatch = a.StatusCodeMatch,
                BodyMatch = a.BodyMatch,
                NightscoutStatusCode = a.NightscoutStatusCode,
                NocturneStatusCode = a.NocturneStatusCode,
                NightscoutResponseTimeMs = a.NightscoutResponseTimeMs,
                NocturneResponseTimeMs = a.NocturneResponseTimeMs,
                TotalProcessingTimeMs = a.TotalProcessingTimeMs,
                Summary = a.Summary,
                SelectedResponseTarget = a.SelectedResponseTarget,
                SelectionReason = a.SelectionReason,
                CriticalDiscrepancyCount = a.CriticalDiscrepancyCount,
                MajorDiscrepancyCount = a.MajorDiscrepancyCount,
                MinorDiscrepancyCount = a.MinorDiscrepancyCount,
                NightscoutMissing = a.NightscoutMissing,
                NocturneMissing = a.NocturneMissing,
                ErrorMessage = a.ErrorMessage,
                Discrepancies = a
                    .Discrepancies.Select(d => new DiscrepancyDetailDto
                    {
                        Id = d.Id,
                        DiscrepancyType = (int)d.DiscrepancyType,
                        Severity = (int)d.Severity,
                        Field = d.Field,
                        NightscoutValue = d.NightscoutValue,
                        NocturneValue = d.NocturneValue,
                        Description = d.Description,
                        RecordedAt = d.RecordedAt,
                    })
                    .ToList(),
            });

            return Ok(analysisResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving discrepancy analyses");
            return StatusCode(500, "Error retrieving discrepancy analyses");
        }
    }

    /// <summary>
    /// Get a specific discrepancy analysis by ID
    /// </summary>
    /// <param name="id">Analysis ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detailed discrepancy analysis</returns>
    [HttpGet("analyses/{id:guid}")]
    public async Task<ActionResult<DiscrepancyAnalysisDto>> GetDiscrepancyAnalysis(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var analyses = await _discrepancyRepository.GetAnalysesAsync(
                count: 1,
                skip: 0,
                cancellationToken: cancellationToken
            );

            var analysis = analyses.FirstOrDefault(a => a.Id == id);
            if (analysis == null)
            {
                return NotFound($"Discrepancy analysis with ID {id} not found");
            }

            var result = new DiscrepancyAnalysisDto
            {
                Id = analysis.Id,
                CorrelationId = analysis.CorrelationId,
                AnalysisTimestamp = analysis.AnalysisTimestamp,
                RequestMethod = analysis.RequestMethod,
                RequestPath = analysis.RequestPath,
                OverallMatch = (int)analysis.OverallMatch,
                StatusCodeMatch = analysis.StatusCodeMatch,
                BodyMatch = analysis.BodyMatch,
                NightscoutStatusCode = analysis.NightscoutStatusCode,
                NocturneStatusCode = analysis.NocturneStatusCode,
                NightscoutResponseTimeMs = analysis.NightscoutResponseTimeMs,
                NocturneResponseTimeMs = analysis.NocturneResponseTimeMs,
                TotalProcessingTimeMs = analysis.TotalProcessingTimeMs,
                Summary = analysis.Summary,
                SelectedResponseTarget = analysis.SelectedResponseTarget,
                SelectionReason = analysis.SelectionReason,
                CriticalDiscrepancyCount = analysis.CriticalDiscrepancyCount,
                MajorDiscrepancyCount = analysis.MajorDiscrepancyCount,
                MinorDiscrepancyCount = analysis.MinorDiscrepancyCount,
                NightscoutMissing = analysis.NightscoutMissing,
                NocturneMissing = analysis.NocturneMissing,
                ErrorMessage = analysis.ErrorMessage,
                Discrepancies = analysis
                    .Discrepancies.Select(d => new DiscrepancyDetailDto
                    {
                        Id = d.Id,
                        DiscrepancyType = (int)d.DiscrepancyType,
                        Severity = (int)d.Severity,
                        Field = d.Field,
                        NightscoutValue = d.NightscoutValue,
                        NocturneValue = d.NocturneValue,
                        Description = d.Description,
                        RecordedAt = d.RecordedAt,
                    })
                    .ToList(),
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving discrepancy analysis {Id}", id);
            return StatusCode(500, "Error retrieving discrepancy analysis");
        }
    }

    /// <summary>
    /// Get real-time compatibility status summary
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current compatibility status</returns>
    [HttpGet("status")]
    public async Task<ActionResult<CompatibilityStatus>> GetCompatibilityStatus(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // Get metrics for the last 24 hours
            var last24Hours = DateTimeOffset.UtcNow.AddHours(-24);
            var metrics = await _discrepancyRepository.GetCompatibilityMetricsAsync(
                last24Hours,
                null,
                cancellationToken
            );

            var status = new CompatibilityStatus
            {
                OverallScore = metrics.CompatibilityScore,
                TotalRequests = metrics.TotalRequests,
                HealthStatus = DetermineHealthStatus(metrics),
                LastUpdated = DateTimeOffset.UtcNow,
                CriticalIssues = metrics.CriticalDifferences,
                MajorIssues = metrics.MajorDifferences,
                MinorIssues = metrics.MinorDifferences,
            };

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving compatibility status");
            return StatusCode(500, "Error retrieving compatibility status");
        }
    }

    private static string DetermineHealthStatus(CompatibilityMetrics metrics)
    {
        if (metrics.TotalRequests == 0)
        {
            return "No Data";
        }

        return metrics.CompatibilityScore switch
        {
            >= 95 => "Excellent",
            >= 85 => "Good",
            >= 70 => "Fair",
            >= 50 => "Poor",
            _ => "Critical",
        };
    }

    /// <summary>
    /// Generate a text-based compatibility report
    /// </summary>
    /// <param name="fromDate">Start date for report (optional)</param>
    /// <param name="toDate">End date for report (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Text-based compatibility report</returns>
    [HttpGet("reports/text")]
    public async Task<ActionResult<string>> GetTextReport(
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                "Generating text report from {FromDate} to {ToDate}",
                fromDate,
                toDate
            );

            var report = await _reportService.GenerateTextReportAsync(
                fromDate,
                toDate,
                cancellationToken
            );

            return Content(report, "text/plain");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating text report");
            return StatusCode(500, "Error generating compatibility report");
        }
    }

    /// <summary>
    /// Generate a migration readiness assessment
    /// </summary>
    /// <param name="fromDate">Start date for assessment (optional)</param>
    /// <param name="toDate">End date for assessment (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Migration readiness assessment</returns>
    [HttpGet("reports/migration-assessment")]
    public async Task<ActionResult<MigrationReadinessReport>> GetMigrationAssessment(
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                "Generating migration assessment from {FromDate} to {ToDate}",
                fromDate,
                toDate
            );

            var assessment = await _reportService.GenerateMigrationAssessmentAsync(
                fromDate,
                toDate,
                cancellationToken
            );

            return Ok(assessment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating migration assessment");
            return StatusCode(500, "Error generating migration assessment");
        }
    }
}
