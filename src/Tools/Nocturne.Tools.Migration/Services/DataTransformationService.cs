using System.Collections.Concurrent;
using MongoDB.Bson;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Tools.Migration.Services.Transformers;

namespace Nocturne.Tools.Migration.Services;

/// <summary>
/// Service that handles conversion of MongoDB documents to PostgreSQL entities
/// with comprehensive data transformation and validation
/// </summary>
public class DataTransformationService : IDataTransformationService
{
    private readonly ConcurrentDictionary<string, BaseDocumentTransformer> _transformers = new();
    private readonly TransformationOptions _defaultOptions;

    public DataTransformationService(TransformationOptions? defaultOptions = null)
    {
        _defaultOptions = defaultOptions ?? new TransformationOptions();
        InitializeTransformers();
    }

    /// <inheritdoc/>
    public async Task<object> TransformDocumentAsync(
        BsonDocument document,
        string collectionName,
        TransformationOptions? options = null
    )
    {
        var effectiveOptions = options ?? _defaultOptions;
        var normalizedCollectionName = collectionName.ToLowerInvariant();

        if (!_transformers.TryGetValue(normalizedCollectionName, out var transformer))
        {
            throw new NotSupportedException(
                $"Collection '{collectionName}' is not supported for transformation"
            );
        }

        try
        {
            // Update transformer options if needed
            if (options != null)
            {
                transformer = CreateTransformerForCollection(normalizedCollectionName, options);
            }

            return await transformer.TransformAsync(document);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to transform document from collection '{collectionName}': {ex.Message}",
                ex
            );
        }
    }

    /// <inheritdoc/>
    public async Task<TransformationValidationResult> ValidateDocumentAsync(
        BsonDocument document,
        string collectionName
    )
    {
        var normalizedCollectionName = collectionName.ToLowerInvariant();

        if (!_transformers.TryGetValue(normalizedCollectionName, out var transformer))
        {
            return new TransformationValidationResult
            {
                IsValid = false,
                Errors = new List<string> { $"Collection '{collectionName}' is not supported" },
                SuggestedFixes = new List<string>
                {
                    $"Supported collections: {string.Join(", ", GetSupportedCollections())}",
                },
            };
        }

        try
        {
            return await transformer.ValidateAsync(document);
        }
        catch (Exception ex)
        {
            return new TransformationValidationResult
            {
                IsValid = false,
                Errors = new List<string> { $"Validation failed: {ex.Message}" },
                SuggestedFixes = new List<string> { "Check document structure and retry" },
            };
        }
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetSupportedCollections()
    {
        return _transformers.Keys.OrderBy(k => k);
    }

    /// <inheritdoc/>
    public TransformationStatistics GetTransformationStatistics(string collectionName)
    {
        var normalizedCollectionName = collectionName.ToLowerInvariant();

        if (!_transformers.TryGetValue(normalizedCollectionName, out var transformer))
        {
            throw new ArgumentException(
                $"Collection '{collectionName}' is not supported",
                nameof(collectionName)
            );
        }

        return transformer.GetStatistics();
    }

    /// <summary>
    /// Gets aggregated transformation statistics across all collections
    /// </summary>
    /// <returns>Aggregated statistics</returns>
    public Dictionary<string, TransformationStatistics> GetAllTransformationStatistics()
    {
        return _transformers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetStatistics());
    }

    /// <summary>
    /// Resets transformation statistics for a specific collection
    /// </summary>
    /// <param name="collectionName">Collection name</param>
    public void ResetStatistics(string collectionName)
    {
        var normalizedCollectionName = collectionName.ToLowerInvariant();

        if (_transformers.TryGetValue(normalizedCollectionName, out var transformer))
        {
            // Recreate transformer to reset statistics
            _transformers[normalizedCollectionName] = CreateTransformerForCollection(
                normalizedCollectionName,
                _defaultOptions
            );
        }
    }

    /// <summary>
    /// Resets transformation statistics for all collections
    /// </summary>
    public void ResetAllStatistics()
    {
        foreach (var collectionName in _transformers.Keys.ToList())
        {
            ResetStatistics(collectionName);
        }
    }

    /// <summary>
    /// Updates transformation options for a specific collection
    /// </summary>
    /// <param name="collectionName">Collection name</param>
    /// <param name="options">New transformation options</param>
    public void UpdateTransformationOptions(string collectionName, TransformationOptions options)
    {
        var normalizedCollectionName = collectionName.ToLowerInvariant();

        if (_transformers.ContainsKey(normalizedCollectionName))
        {
            _transformers[normalizedCollectionName] = CreateTransformerForCollection(
                normalizedCollectionName,
                options
            );
        }
    }

    /// <summary>
    /// Gets detailed validation summary for multiple documents
    /// </summary>
    /// <param name="documents">Documents to validate</param>
    /// <param name="collectionName">Collection name</param>
    /// <returns>Validation summary</returns>
    public async Task<BatchValidationSummary> ValidateDocumentBatchAsync(
        IEnumerable<BsonDocument> documents,
        string collectionName
    )
    {
        var summary = new BatchValidationSummary
        {
            CollectionName = collectionName,
            TotalDocuments = 0,
            ValidDocuments = 0,
            InvalidDocuments = 0,
            DocumentsWithWarnings = 0,
            CommonErrors = new Dictionary<string, int>(),
            CommonWarnings = new Dictionary<string, int>(),
        };

        foreach (var document in documents)
        {
            summary.TotalDocuments++;

            var validation = await ValidateDocumentAsync(document, collectionName);

            if (validation.IsValid)
            {
                summary.ValidDocuments++;

                if (validation.Warnings.Count > 0)
                {
                    summary.DocumentsWithWarnings++;

                    foreach (var warning in validation.Warnings)
                    {
                        summary.CommonWarnings.TryGetValue(warning, out var count);
                        summary.CommonWarnings[warning] = count + 1;
                    }
                }
            }
            else
            {
                summary.InvalidDocuments++;

                foreach (var error in validation.Errors)
                {
                    summary.CommonErrors.TryGetValue(error, out var count);
                    summary.CommonErrors[error] = count + 1;
                }
            }
        }

        return summary;
    }

    private void InitializeTransformers()
    {
        // Initialize transformers for all supported collection types
        _transformers["entries"] = new EntryTransformer(_defaultOptions);
        _transformers["treatments"] = new TreatmentTransformer(_defaultOptions);
        _transformers["profiles"] = new ProfileTransformer(_defaultOptions);
        _transformers["devicestatus"] = new DeviceStatusTransformer(_defaultOptions);
        _transformers["settings"] = new SettingsTransformer(_defaultOptions);
        _transformers["food"] = new FoodTransformer(_defaultOptions);
        _transformers["activity"] = new ActivityTransformer(_defaultOptions);
        // Auth transformer commented out - main app doesn't have AuthEntity
        // _transformers["auth"] = new AuthTransformer(_defaultOptions);
    }

    private BaseDocumentTransformer CreateTransformerForCollection(
        string collectionName,
        TransformationOptions options
    )
    {
        return collectionName.ToLowerInvariant() switch
        {
            "entries" => new EntryTransformer(options),
            "treatments" => new TreatmentTransformer(options),
            "profiles" => new ProfileTransformer(options),
            "devicestatus" => new DeviceStatusTransformer(options),
            "settings" => new SettingsTransformer(options),
            "food" => new FoodTransformer(options),
            "activity" => new ActivityTransformer(options),
            // Auth transformer commented out - main app doesn't have AuthEntity
            // "auth" => new AuthTransformer(options),
            _ => throw new NotSupportedException($"Collection '{collectionName}' is not supported"),
        };
    }
}

/// <summary>
/// Summary of batch validation results
/// </summary>
public class BatchValidationSummary
{
    /// <summary>
    /// Collection name
    /// </summary>
    public required string CollectionName { get; init; }

    /// <summary>
    /// Total documents validated
    /// </summary>
    public int TotalDocuments { get; set; }

    /// <summary>
    /// Number of valid documents
    /// </summary>
    public int ValidDocuments { get; set; }

    /// <summary>
    /// Number of invalid documents
    /// </summary>
    public int InvalidDocuments { get; set; }

    /// <summary>
    /// Number of documents with warnings
    /// </summary>
    public int DocumentsWithWarnings { get; set; }

    /// <summary>
    /// Most common validation errors and their counts
    /// </summary>
    public Dictionary<string, int> CommonErrors { get; set; } = new();

    /// <summary>
    /// Most common validation warnings and their counts
    /// </summary>
    public Dictionary<string, int> CommonWarnings { get; set; } = new();

    /// <summary>
    /// Validation success rate as percentage
    /// </summary>
    public double SuccessRate =>
        TotalDocuments > 0 ? (double)ValidDocuments / TotalDocuments * 100 : 0;
}
