namespace Nocturne.Tools.Migration.Services;

/// <summary>
/// Service for analyzing MongoDB collections to gather statistics
/// </summary>
public interface ICollectionAnalysisService
{
    /// <summary>
    /// Analyze a single collection to gather document counts and date ranges
    /// </summary>
    /// <param name="collectionName">Name of the collection to analyze</param>
    /// <param name="startDate">Optional start date filter</param>
    /// <param name="endDate">Optional end date filter</param>
    /// <returns>Collection statistics</returns>
    Task<CollectionAnalysisStatistics> AnalyzeCollectionAsync(
        string collectionName,
        DateTime? startDate = null,
        DateTime? endDate = null
    );

    /// <summary>
    /// Analyze all collections in the database
    /// </summary>
    /// <param name="startDate">Optional start date filter</param>
    /// <param name="endDate">Optional end date filter</param>
    /// <returns>List of collection statistics</returns>
    Task<List<CollectionAnalysisStatistics>> AnalyzeAllCollectionsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null
    );
}

/// <summary>
/// Statistics for a MongoDB collection analysis
/// </summary>
/// <param name="CollectionName">Name of the collection</param>
/// <param name="DocumentCount">Total number of documents</param>
/// <param name="EarliestDate">Earliest timestamp found in the collection</param>
/// <param name="LatestDate">Latest timestamp found in the collection</param>
public record CollectionAnalysisStatistics(
    string CollectionName,
    long DocumentCount,
    DateTime? EarliestDate,
    DateTime? LatestDate
);
