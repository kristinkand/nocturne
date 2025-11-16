using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Nocturne.Tools.Migration.Services;

/// <summary>
/// Service for analyzing MongoDB collections to gather statistics
/// </summary>
public class CollectionAnalysisService : ICollectionAnalysisService
{
    private readonly ILogger<CollectionAnalysisService> _logger;
    private readonly IMongoDatabase _mongoDatabase;

    // Map of collection names to their primary date field
    private static readonly Dictionary<string, string> CollectionDateFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "entries", "date" },
            { "treatments", "created_at" },
            { "devicestatus", "created_at" },
            { "profile", "created_at" },
            { "food", "created_at" },
            { "activity", "created_at" },
        };

    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionAnalysisService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="mongoConnectionString">MongoDB connection string.</param>
    /// <param name="mongoDatabaseName">MongoDB database name.</param>
    public CollectionAnalysisService(
        ILogger<CollectionAnalysisService> logger,
        string mongoConnectionString,
        string mongoDatabaseName
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (string.IsNullOrWhiteSpace(mongoConnectionString))
            throw new ArgumentException(
                "MongoDB connection string cannot be null or empty",
                nameof(mongoConnectionString)
            );
        if (string.IsNullOrWhiteSpace(mongoDatabaseName))
            throw new ArgumentException(
                "MongoDB database name cannot be null or empty",
                nameof(mongoDatabaseName)
            );

        var client = new MongoClient(mongoConnectionString);
        _mongoDatabase = client.GetDatabase(mongoDatabaseName);
    }

    /// <inheritdoc />
    public async Task<CollectionAnalysisStatistics> AnalyzeCollectionAsync(
        string collectionName,
        DateTime? startDate = null,
        DateTime? endDate = null
    )
    {
        try
        {
            var collection = _mongoDatabase.GetCollection<BsonDocument>(collectionName);

            // Build filter for date range if applicable
            var filter = BuildDateFilter(collectionName, startDate, endDate);

            // Get document count
            var count = await collection.CountDocumentsAsync(filter);

            // Get date range if collection has date field
            DateTime? earliestDate = null;
            DateTime? latestDate = null;

            if (CollectionDateFields.TryGetValue(collectionName, out var dateField))
            {
                var sortAscending = Builders<BsonDocument>.Sort.Ascending(dateField);
                var sortDescending = Builders<BsonDocument>.Sort.Descending(dateField);

                var earliest = await collection
                    .Find(filter)
                    .Sort(sortAscending)
                    .Limit(1)
                    .FirstOrDefaultAsync();

                var latest = await collection
                    .Find(filter)
                    .Sort(sortDescending)
                    .Limit(1)
                    .FirstOrDefaultAsync();

                if (earliest != null && earliest.Contains(dateField))
                {
                    earliestDate = ExtractDate(earliest[dateField]);
                }

                if (latest != null && latest.Contains(dateField))
                {
                    latestDate = ExtractDate(latest[dateField]);
                }
            }

            return new CollectionAnalysisStatistics(collectionName, count, earliestDate, latestDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to analyze collection {CollectionName}: {ErrorMessage}",
                collectionName,
                ex.Message
            );
            return new CollectionAnalysisStatistics(collectionName, 0, null, null);
        }
    }

    /// <inheritdoc />
    public async Task<List<CollectionAnalysisStatistics>> AnalyzeAllCollectionsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null
    )
    {
        try
        {
            var collectionNames = await (
                await _mongoDatabase.ListCollectionNamesAsync()
            ).ToListAsync();

            var statistics = new List<CollectionAnalysisStatistics>();

            foreach (var collectionName in collectionNames)
            {
                // Skip system collections
                if (collectionName.StartsWith("system."))
                {
                    continue;
                }

                var stats = await AnalyzeCollectionAsync(collectionName, startDate, endDate);
                statistics.Add(stats);
            }

            return statistics.OrderByDescending(s => s.DocumentCount).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to analyze collections: {ErrorMessage}",
                ex.Message
            );
            throw;
        }
    }

    /// <summary>
    /// Build MongoDB filter for date range
    /// </summary>
    private FilterDefinition<BsonDocument> BuildDateFilter(
        string collectionName,
        DateTime? startDate,
        DateTime? endDate
    )
    {
        var filter = Builders<BsonDocument>.Filter.Empty;

        if (!CollectionDateFields.TryGetValue(collectionName, out var dateField))
        {
            return filter;
        }

        if (startDate.HasValue)
        {
            filter &= Builders<BsonDocument>.Filter.Gte(dateField, startDate.Value);
        }

        if (endDate.HasValue)
        {
            filter &= Builders<BsonDocument>.Filter.Lte(dateField, endDate.Value);
        }

        return filter;
    }

    /// <summary>
    /// Extract DateTime from BsonValue (handles various BSON date representations)
    /// </summary>
    private DateTime? ExtractDate(BsonValue value)
    {
        try
        {
            if (value.BsonType == BsonType.DateTime)
            {
                return value.ToUniversalTime();
            }
            else if (value.BsonType == BsonType.Int64)
            {
                // Unix milliseconds timestamp
                return DateTimeOffset.FromUnixTimeMilliseconds(value.AsInt64).UtcDateTime;
            }
            else if (value.BsonType == BsonType.String && DateTime.TryParse(value.AsString, out var parsedDate))
            {
                return parsedDate.ToUniversalTime();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract date from BsonValue: {Value}", value);
        }

        return null;
    }
}
