using System.Text.Json;
using MongoDB.Bson;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Tools.Migration.Services.Transformers;

/// <summary>
/// Base class for document transformers that provides common functionality
/// </summary>
public abstract class BaseDocumentTransformer
{
    protected readonly TransformationOptions _options;
    protected readonly TransformationStatistics _statistics;

    protected BaseDocumentTransformer(string collectionName, TransformationOptions? options = null)
    {
        _options = options ?? new TransformationOptions();
        _statistics = new TransformationStatistics { CollectionName = collectionName };
    }

    /// <summary>
    /// Transforms a MongoDB document to a PostgreSQL entity
    /// </summary>
    /// <param name="document">MongoDB BSON document</param>
    /// <returns>Transformed PostgreSQL entity</returns>
    public abstract Task<object> TransformAsync(BsonDocument document);

    /// <summary>
    /// Validates a MongoDB document before transformation
    /// </summary>
    /// <param name="document">MongoDB BSON document</param>
    /// <returns>Validation result</returns>
    public abstract Task<TransformationValidationResult> ValidateAsync(BsonDocument document);

    /// <summary>
    /// Gets transformation statistics
    /// </summary>
    /// <returns>Transformation statistics</returns>
    public TransformationStatistics GetStatistics() => _statistics;

    /// <summary>
    /// Converts MongoDB ObjectId to PostgreSQL UUID
    /// </summary>
    /// <param name="objectId">MongoDB ObjectId</param>
    /// <returns>PostgreSQL UUID</returns>
    protected Guid ConvertObjectIdToGuid(string? objectId)
    {
        if (string.IsNullOrEmpty(objectId))
            return Guid.CreateVersion7();

        // If preserving original IDs, create deterministic GUID from ObjectId
        if (_options.PreserveOriginalIds && objectId.Length == 24)
        {
            // Convert ObjectId to GUID using a consistent method
            var bytes = new byte[16];
            var objectIdBytes = Convert.FromHexString(objectId);

            // Copy first 12 bytes of ObjectId and pad with zeros
            Array.Copy(objectIdBytes, 0, bytes, 0, Math.Min(objectIdBytes.Length, 12));

            return new Guid(bytes);
        }

        return Guid.CreateVersion7();
    }

    /// <summary>
    /// Converts various timestamp formats to DateTime
    /// </summary>
    /// <param name="value">BSON value containing timestamp</param>
    /// <param name="defaultValue">Default value if conversion fails</param>
    /// <returns>Converted DateTime</returns>
    protected DateTime ConvertToDateTime(BsonValue value, DateTime? defaultValue = null)
    {
        var fallback = defaultValue ?? DateTime.UtcNow;

        if (value == BsonNull.Value || value == null)
            return fallback;

        try
        {
            // Handle milliseconds since Unix epoch
            if (value.IsInt64 && value.AsInt64 > 0)
            {
                var timestamp = value.AsInt64;

                // Check if it's milliseconds (typical for Nightscout) or seconds
                if (timestamp > 1_000_000_000_000) // Milliseconds
                {
                    return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime;
                }
                else // Seconds
                {
                    return DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
                }
            }

            // Handle DateTime objects
            if (value.IsValidDateTime)
            {
                return value.ToUniversalTime();
            }

            // Handle string dates
            if (value.IsString)
            {
                var dateString = value.AsString;
                if (
                    DateTime.TryParse(
                        dateString,
                        null,
                        System.Globalization.DateTimeStyles.RoundtripKind,
                        out var parsedDate
                    )
                )
                {
                    return DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
                }
            }

            return fallback;
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>
    /// Converts BSON value to nullable double
    /// </summary>
    /// <param name="value">BSON value</param>
    /// <returns>Nullable double</returns>
    protected double? ToNullableDouble(BsonValue value)
    {
        if (value == BsonNull.Value || value == null)
            return null;

        try
        {
            // Handle different BSON numeric types
            return value.BsonType switch
            {
                BsonType.Double => value.AsDouble,
                BsonType.Int32 => (double)value.AsInt32,
                BsonType.Int64 => (double)value.AsInt64,
                BsonType.Decimal128 => (double)value.AsDecimal,
                BsonType.String when double.TryParse(value.AsString, out var result) => result,
                _ => null,
            };
        }
        catch
        {
            // Fallback: try to parse as string
            if (value.IsString && double.TryParse(value.AsString, out var result))
                return result;
            return null;
        }
    }

    /// <summary>
    /// Converts BSON value to nullable int
    /// </summary>
    /// <param name="value">BSON value</param>
    /// <returns>Nullable int</returns>
    protected int? ToNullableInt32(BsonValue value)
    {
        if (value == BsonNull.Value || value == null)
            return null;

        try
        {
            return value.AsInt32;
        }
        catch
        {
            if (value.IsString && int.TryParse(value.AsString, out var result))
                return result;
            return null;
        }
    }

    /// <summary>
    /// Converts BSON value to string
    /// </summary>
    /// <param name="value">BSON value</param>
    /// <param name="maxLength">Maximum string length</param>
    /// <returns>String value</returns>
    protected string? ToString(BsonValue value, int? maxLength = null)
    {
        if (value == BsonNull.Value || value == null)
            return null;

        var result = value.ToString();

        if (maxLength.HasValue && result?.Length > maxLength.Value)
        {
            result = result[..maxLength.Value];
        }

        return result;
    }

    /// <summary>
    /// Converts complex BSON structure to JSON for JSONB storage
    /// </summary>
    /// <param name="value">BSON value</param>
    /// <returns>JSON string for PostgreSQL JSONB column</returns>
    protected string? ToJsonB(BsonValue value)
    {
        if (value == BsonNull.Value || value == null)
            return null;

        try
        {
            // Convert BSON to dictionary/object structure that can be serialized to JSON
            var jsonObject = ConvertBsonToObject(value, 0);
            return JsonSerializer.Serialize(
                jsonObject,
                new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                }
            );
        }
        catch
        {
            // Fallback to simple string representation
            return value.ToString();
        }
    }

    /// <summary>
    /// Converts BSON value to .NET object for JSON serialization
    /// </summary>
    /// <param name="value">BSON value</param>
    /// <param name="depth">Current nesting depth</param>
    /// <returns>.NET object</returns>
    private object? ConvertBsonToObject(BsonValue value, int depth)
    {
        if (depth > _options.MaxNestingDepth)
            return value.ToString();

        return value.BsonType switch
        {
            BsonType.Null => null,
            BsonType.Boolean => value.AsBoolean,
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => value.AsInt64,
            BsonType.Double => value.AsDouble,
            BsonType.String => value.AsString,
            BsonType.DateTime => value.ToUniversalTime(),
            BsonType.ObjectId => value.AsObjectId.ToString(),
            BsonType.Array => value
                .AsBsonArray.Select(item => ConvertBsonToObject(item, depth + 1))
                .ToArray(),
            BsonType.Document => value.AsBsonDocument.ToDictionary(
                element => element.Name,
                element => ConvertBsonToObject(element.Value, depth + 1)
            ),
            _ => value.ToString(),
        };
    }

    /// <summary>
    /// Updates field statistics
    /// </summary>
    /// <param name="fieldName">Field name</param>
    /// <param name="value">BSON value</param>
    /// <param name="transformationSucceeded">Whether transformation succeeded</param>
    protected void UpdateFieldStatistics(
        string fieldName,
        BsonValue value,
        bool transformationSucceeded
    )
    {
        if (!_statistics.FieldStats.TryGetValue(fieldName, out var stats))
        {
            stats = new FieldTransformationStats { FieldName = fieldName };
            _statistics.FieldStats[fieldName] = stats;
        }

        if (value == BsonNull.Value)
        {
            stats.Null++;
        }
        else
        {
            stats.Present++;

            var typeName = value.BsonType.ToString();
            stats.DataTypes.TryGetValue(typeName, out var count);
            stats.DataTypes[typeName] = count + 1;
        }

        if (!transformationSucceeded)
        {
            stats.TransformationFailed++;
        }
    }

    /// <summary>
    /// Updates field statistics for missing field
    /// </summary>
    /// <param name="fieldName">Field name</param>
    protected void UpdateMissingFieldStatistics(string fieldName)
    {
        if (!_statistics.FieldStats.TryGetValue(fieldName, out var stats))
        {
            stats = new FieldTransformationStats { FieldName = fieldName };
            _statistics.FieldStats[fieldName] = stats;
        }

        stats.Missing++;
    }

    /// <summary>
    /// Records transformation success
    /// </summary>
    protected void RecordTransformationSuccess()
    {
        _statistics.TotalProcessed++;
        _statistics.SuccessfullyTransformed++;
    }

    /// <summary>
    /// Records transformation failure
    /// </summary>
    /// <param name="error">Error message</param>
    protected void RecordTransformationFailure(string error)
    {
        _statistics.TotalProcessed++;
        _statistics.Failed++;

        _statistics.CommonErrors.TryGetValue(error, out var count);
        _statistics.CommonErrors[error] = count + 1;
    }

    /// <summary>
    /// Records transformation warning
    /// </summary>
    protected void RecordTransformationWarning()
    {
        _statistics.WithWarnings++;
    }

    /// <summary>
    /// Filters a dictionary to exclude null values if PreserveNullProperties is false
    /// </summary>
    /// <param name="properties">Dictionary to filter</param>
    /// <returns>Filtered dictionary</returns>
    protected Dictionary<string, object?> FilterNullProperties(
        Dictionary<string, object?> properties
    )
    {
        if (_options.PreserveNullProperties)
        {
            return properties;
        }

        return properties
            .Where(kvp => kvp.Value != null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Converts a BSON DateTime value to ISO string format for PostgreSQL storage
    /// </summary>
    /// <param name="value">BSON value to convert</param>
    /// <returns>ISO string representation or null</returns>
    protected string? ConvertToDateTimeString(BsonValue value)
    {
        if (value == BsonNull.Value)
            return null;

        try
        {
            var dateTime = ConvertToDateTime(value);
            return dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts nullable double to required double with safe fallback
    /// </summary>
    /// <param name="value">Nullable double value</param>
    /// <returns>Non-null double value</returns>
    protected double ConvertToRequiredDouble(double? value)
    {
        return value ?? 0.0;
    }

    /// <summary>
    /// Converts nullable int to required int with safe fallback
    /// </summary>
    /// <param name="value">Nullable int value</param>
    /// <returns>Non-null int value</returns>
    protected int ConvertToRequiredInt(int? value)
    {
        return value ?? 0;
    }
}
