using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts;

/// <summary>
/// Service for processing and sanitizing treatment data to match legacy Nightscout behavior
/// </summary>
public interface ITreatmentProcessingService
{
    /// <summary>
    /// Process a list of treatments by sanitizing HTML content and converting timestamps
    /// </summary>
    /// <param name="treatments">The treatments to process</param>
    /// <returns>Processed treatments with sanitized content and converted timestamps</returns>
    IEnumerable<Treatment> ProcessTreatments(IEnumerable<Treatment> treatments);

    /// <summary>
    /// Sanitize HTML content in treatment notes and other text fields
    /// </summary>
    /// <param name="htmlContent">The HTML content to sanitize</param>
    /// <returns>Sanitized HTML content safe for display</returns>
    string SanitizeHtml(string? htmlContent);

    /// <summary>
    /// Convert timezone-aware timestamps to UTC and set utcOffset
    /// </summary>
    /// <param name="treatment">The treatment to process</param>
    void ProcessTimestamp(Treatment treatment);
}
