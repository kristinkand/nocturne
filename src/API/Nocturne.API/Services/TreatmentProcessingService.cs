using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Services;

/// <summary>
/// Service for processing and sanitizing treatment data to match legacy Nightscout behavior
/// </summary>
public class TreatmentProcessingService : ITreatmentProcessingService
{
    private readonly IDocumentProcessingService _documentProcessingService;
    private readonly ILogger<TreatmentProcessingService> _logger;

    public TreatmentProcessingService(
        IDocumentProcessingService documentProcessingService,
        ILogger<TreatmentProcessingService> logger
    )
    {
        _documentProcessingService = documentProcessingService;
        _logger = logger;
    }

    /// <inheritdoc />
    public IEnumerable<Treatment> ProcessTreatments(IEnumerable<Treatment> treatments)
    {
        _logger.LogDebug(
            "Processing {Count} treatments with treatment-specific logic",
            treatments.Count()
        );

        // Use the generic document processing service for sanitization and timestamp conversion
        var processedTreatments = _documentProcessingService.ProcessDocuments(
            treatments
        );

        _logger.LogDebug("Processed treatments with generic document processing");

        return processedTreatments;
    }

    /// <inheritdoc />
    public string SanitizeHtml(string? htmlContent)
    {
        return _documentProcessingService.SanitizeHtml(htmlContent);
    }

    /// <inheritdoc />
    public void ProcessTimestamp(Treatment treatment)
    {
        _documentProcessingService.ProcessTimestamp(treatment);
    }
}
