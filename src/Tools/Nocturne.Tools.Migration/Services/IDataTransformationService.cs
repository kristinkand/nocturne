using MongoDB.Bson;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Tools.Migration.Services;

/// <summary>
/// Interface for data transformation service that handles conversion of MongoDB documents to PostgreSQL entities
/// </summary>
public interface IDataTransformationService
{
    /// <summary>
    /// Transforms a MongoDB document to a PostgreSQL entity based on collection type
    /// </summary>
    /// <param name="document">MongoDB BSON document</param>
    /// <param name="collectionName">Name of the source collection</param>
    /// <param name="options">Transformation options</param>
    /// <returns>Transformed PostgreSQL entity</returns>
    Task<object> TransformDocumentAsync(
        BsonDocument document,
        string collectionName,
        TransformationOptions? options = null
    );

    /// <summary>
    /// Validates a MongoDB document before transformation
    /// </summary>
    /// <param name="document">MongoDB BSON document</param>
    /// <param name="collectionName">Name of the source collection</param>
    /// <returns>Validation result</returns>
    Task<TransformationValidationResult> ValidateDocumentAsync(
        BsonDocument document,
        string collectionName
    );

    /// <summary>
    /// Gets supported collection types
    /// </summary>
    /// <returns>List of supported collection names</returns>
    IEnumerable<string> GetSupportedCollections();

    /// <summary>
    /// Gets transformation statistics for a collection
    /// </summary>
    /// <param name="collectionName">Name of the collection</param>
    /// <returns>Transformation statistics</returns>
    TransformationStatistics GetTransformationStatistics(string collectionName);
}

/// <summary>
/// Options for data transformation
/// </summary>
public class TransformationOptions
{
    /// <summary>
    /// Whether to preserve original MongoDB ObjectIds
    /// </summary>
    public bool PreserveOriginalIds { get; set; } = true;

    /// <summary>
    /// Whether to generate new UUIDs for entities
    /// </summary>
    public bool GenerateNewUuids { get; set; } = true;

    /// <summary>
    /// Default timezone for timestamp conversions
    /// </summary>
    public TimeZoneInfo? DefaultTimeZone { get; set; } = TimeZoneInfo.Utc;

    /// <summary>
    /// Whether to preserve null values in additional_properties fields
    /// </summary>
    public bool PreserveNullProperties { get; set; } = false;

    /// <summary>
    /// Whether to validate data during transformation
    /// </summary>
    public bool ValidateData { get; set; } = true;

    /// <summary>
    /// Whether to handle missing fields gracefully
    /// </summary>
    public bool HandleMissingFields { get; set; } = true;

    /// <summary>
    /// Custom transformation rules per collection
    /// </summary>
    public Dictionary<string, Dictionary<string, object>> CustomRules { get; set; } = new();

    /// <summary>
    /// Maximum depth for nested object serialization
    /// </summary>
    public int MaxNestingDepth { get; set; } = 10;
}

/// <summary>
/// Result of document validation
/// </summary>
public class TransformationValidationResult
{
    /// <summary>
    /// Whether the document is valid for transformation
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Validation errors
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// Validation warnings
    /// </summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>
    /// Suggested fixes for validation issues
    /// </summary>
    public List<string> SuggestedFixes { get; init; } = new();
}

/// <summary>
/// Statistics for transformation operations
/// </summary>
public class TransformationStatistics
{
    /// <summary>
    /// Collection name
    /// </summary>
    public required string CollectionName { get; init; }

    /// <summary>
    /// Total documents processed
    /// </summary>
    public long TotalProcessed { get; set; }

    /// <summary>
    /// Successfully transformed documents
    /// </summary>
    public long SuccessfullyTransformed { get; set; }

    /// <summary>
    /// Failed transformations
    /// </summary>
    public long Failed { get; set; }

    /// <summary>
    /// Documents with validation warnings
    /// </summary>
    public long WithWarnings { get; set; }

    /// <summary>
    /// Average transformation time per document
    /// </summary>
    public TimeSpan AverageTransformationTime { get; set; }

    /// <summary>
    /// Most common transformation errors
    /// </summary>
    public Dictionary<string, int> CommonErrors { get; set; } = new();

    /// <summary>
    /// Field-level transformation statistics
    /// </summary>
    public Dictionary<string, FieldTransformationStats> FieldStats { get; set; } = new();
}

/// <summary>
/// Statistics for field-level transformations
/// </summary>
public class FieldTransformationStats
{
    /// <summary>
    /// Field name
    /// </summary>
    public required string FieldName { get; init; }

    /// <summary>
    /// Number of times field was present
    /// </summary>
    public long Present { get; set; }

    /// <summary>
    /// Number of times field was missing
    /// </summary>
    public long Missing { get; set; }

    /// <summary>
    /// Number of times field had null value
    /// </summary>
    public long Null { get; set; }

    /// <summary>
    /// Number of times field transformation failed
    /// </summary>
    public long TransformationFailed { get; set; }

    /// <summary>
    /// Most common data types encountered
    /// </summary>
    public Dictionary<string, int> DataTypes { get; set; } = new();
}
