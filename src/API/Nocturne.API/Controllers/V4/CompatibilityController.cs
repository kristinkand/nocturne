using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Nocturne.API.Configuration;
using Nocturne.API.Services.Compatibility;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Repositories;

namespace Nocturne.API.Controllers.V4;

/// <summary>
/// API controller for compatibility dashboard data
/// </summary>
[ApiController]
[Route("api/v4/compatibility")]
public class CompatibilityController : ControllerBase
{
    private readonly IDiscrepancyPersistenceService _persistenceService;
    private readonly DiscrepancyAnalysisRepository _repository;
    private readonly ICompatibilityReportService _reportService;
    private readonly CompatibilityProxyConfiguration _configuration;
    private readonly ILogger<CompatibilityController> _logger;

    /// <summary>
    /// Initializes a new instance of the CompatibilityController class
    /// </summary>
    public CompatibilityController(
        IDiscrepancyPersistenceService persistenceService,
        DiscrepancyAnalysisRepository repository,
        ICompatibilityReportService reportService,
        IOptions<CompatibilityProxyConfiguration> configuration,
        ILogger<CompatibilityController> logger
    )
    {
        _persistenceService = persistenceService;
        _repository = repository;
        _reportService = reportService;
        _configuration = configuration.Value;
        _logger = logger;
    }

    /// <summary>
    /// Get current proxy configuration
    /// </summary>
    [HttpGet("config")]
    [ProducesResponseType(typeof(ProxyConfigurationDto), StatusCodes.Status200OK)]
    public ActionResult<ProxyConfigurationDto> GetConfiguration()
    {
        return Ok(
            new ProxyConfigurationDto
            {
                NightscoutUrl = _configuration.NightscoutUrl,
                NocturneUrl = _configuration.NocturneUrl,
                DefaultStrategy = _configuration.DefaultStrategy.ToString(),
                EnableDetailedLogging = _configuration.EnableDetailedLogging,
            }
        );
    }

    /// <summary>
    /// Get overall compatibility metrics
    /// </summary>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(CompatibilityMetrics), StatusCodes.Status200OK)]
    public async Task<ActionResult<CompatibilityMetrics>> GetMetrics(
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var metrics = await _persistenceService.GetCompatibilityMetricsAsync(
                fromDate,
                toDate,
                cancellationToken
            );
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving compatibility metrics");
            return StatusCode(500, new { error = "Failed to retrieve metrics" });
        }
    }

    /// <summary>
    /// Get per-endpoint compatibility metrics
    /// </summary>
    [HttpGet("endpoints")]
    [ProducesResponseType(typeof(IEnumerable<EndpointMetrics>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EndpointMetrics>>> GetEndpointMetrics(
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var metrics = await _persistenceService.GetEndpointMetricsAsync(
                fromDate,
                toDate,
                cancellationToken
            );
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving endpoint metrics");
            return StatusCode(500, new { error = "Failed to retrieve endpoint metrics" });
        }
    }

    /// <summary>
    /// Get list of analyses with filtering and pagination
    /// </summary>
    [HttpGet("analyses")]
    [ProducesResponseType(typeof(AnalysesListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalysesListResponse>> GetAnalyses(
        [FromQuery] string? requestPath = null,
        [FromQuery] ResponseMatchType? overallMatch = null,
        [FromQuery] string? requestMethod = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        [FromQuery] int count = 100,
        [FromQuery] int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var analyses = await _repository.GetAnalysesAsync(
                requestPath,
                overallMatch.HasValue ? (int)overallMatch.Value : null,
                fromDate,
                toDate,
                count,
                skip,
                cancellationToken
            );

            var analysisItems = analyses
                .Select(a => new AnalysisListItemDto
                {
                    Id = a.Id,
                    CorrelationId = a.CorrelationId,
                    AnalysisTimestamp = a.AnalysisTimestamp,
                    RequestMethod = a.RequestMethod,
                    RequestPath = a.RequestPath,
                    OverallMatch = a.OverallMatch,
                    StatusCodeMatch = a.StatusCodeMatch,
                    BodyMatch = a.BodyMatch,
                    NightscoutStatusCode = a.NightscoutStatusCode,
                    NocturneStatusCode = a.NocturneStatusCode,
                    NightscoutResponseTimeMs = a.NightscoutResponseTimeMs,
                    NocturneResponseTimeMs = a.NocturneResponseTimeMs,
                    TotalProcessingTimeMs = a.TotalProcessingTimeMs,
                    Summary = a.Summary,
                    CriticalDiscrepancyCount = a.CriticalDiscrepancyCount,
                    MajorDiscrepancyCount = a.MajorDiscrepancyCount,
                    MinorDiscrepancyCount = a.MinorDiscrepancyCount,
                    NightscoutMissing = a.NightscoutMissing,
                    NocturneMissing = a.NocturneMissing,
                })
                .ToList();

            return Ok(new AnalysesListResponse { Analyses = analysisItems, Total = analysisItems.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving analyses");
            return StatusCode(500, new { error = "Failed to retrieve analyses" });
        }
    }

    /// <summary>
    /// Get detailed analysis by ID
    /// </summary>
    [HttpGet("analyses/{id}")]
    [ProducesResponseType(typeof(AnalysisDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnalysisDetailDto>> GetAnalysisDetail(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var analyses = await _repository.GetAnalysesAsync(
                null,
                null,
                null,
                null,
                1,
                0,
                cancellationToken
            );

            var analysis = analyses.FirstOrDefault(a => a.Id == id);

            if (analysis == null)
            {
                return NotFound(new { error = "Analysis not found" });
            }

            var detail = new AnalysisDetailDto
            {
                Id = analysis.Id,
                CorrelationId = analysis.CorrelationId,
                AnalysisTimestamp = analysis.AnalysisTimestamp,
                RequestMethod = analysis.RequestMethod,
                RequestPath = analysis.RequestPath,
                OverallMatch = analysis.OverallMatch,
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

            return Ok(detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving analysis detail for {Id}", id);
            return StatusCode(500, new { error = "Failed to retrieve analysis detail" });
        }
    }

    /// <summary>
    /// Get migration readiness assessment
    /// </summary>
    [HttpGet("migration-assessment")]
    [ProducesResponseType(typeof(MigrationReadinessReport), StatusCodes.Status200OK)]
    public async Task<ActionResult<MigrationReadinessReport>> GetMigrationAssessment(
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var report = await _reportService.GenerateMigrationAssessmentAsync(
                fromDate,
                toDate,
                cancellationToken
            );
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating migration assessment");
            return StatusCode(500, new { error = "Failed to generate migration assessment" });
        }
    }

    /// <summary>
    /// Get text report
    /// </summary>
    [HttpGet("report")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<ActionResult<string>> GetTextReport(
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var report = await _reportService.GenerateTextReportAsync(
                fromDate,
                toDate,
                cancellationToken
            );
            return Ok(new { report });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating text report");
            return StatusCode(500, new { error = "Failed to generate text report" });
        }
    }
}

/// <summary>
/// Proxy configuration DTO
/// </summary>
public class ProxyConfigurationDto
{
    public string NightscoutUrl { get; set; } = string.Empty;
    public string NocturneUrl { get; set; } = string.Empty;
    public string DefaultStrategy { get; set; } = string.Empty;
    public bool EnableDetailedLogging { get; set; }
}

/// <summary>
/// Analysis list item DTO
/// </summary>
public class AnalysisListItemDto
{
    public Guid Id { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset AnalysisTimestamp { get; set; }
    public string RequestMethod { get; set; } = string.Empty;
    public string RequestPath { get; set; } = string.Empty;
    public ResponseMatchType OverallMatch { get; set; }
    public bool StatusCodeMatch { get; set; }
    public bool BodyMatch { get; set; }
    public int? NightscoutStatusCode { get; set; }
    public int? NocturneStatusCode { get; set; }
    public long? NightscoutResponseTimeMs { get; set; }
    public long? NocturneResponseTimeMs { get; set; }
    public long TotalProcessingTimeMs { get; set; }
    public string Summary { get; set; } = string.Empty;
    public int CriticalDiscrepancyCount { get; set; }
    public int MajorDiscrepancyCount { get; set; }
    public int MinorDiscrepancyCount { get; set; }
    public bool NightscoutMissing { get; set; }
    public bool NocturneMissing { get; set; }
}

/// <summary>
/// Analyses list response
/// </summary>
public class AnalysesListResponse
{
    public List<AnalysisListItemDto> Analyses { get; set; } = new();
    public int Total { get; set; }
}

/// <summary>
/// Analysis detail DTO
/// </summary>
public class AnalysisDetailDto
{
    public Guid Id { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset AnalysisTimestamp { get; set; }
    public string RequestMethod { get; set; } = string.Empty;
    public string RequestPath { get; set; } = string.Empty;
    public ResponseMatchType OverallMatch { get; set; }
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
