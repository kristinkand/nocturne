using MongoDB.Bson;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Tools.Migration.Services.Transformers;

/// <summary>
/// Transformer for Entry (glucose readings) documents
/// Handles multiple glucose value formats, direction enums, and complex timestamp handling
/// </summary>
public class EntryTransformer : BaseDocumentTransformer
{
    public EntryTransformer(TransformationOptions? options = null)
        : base("entries", options) { }

    public override async Task<object> TransformAsync(BsonDocument document)
    {
        try
        {
            var entity = new EntryEntity();

            // Transform ID
            var originalId = document.GetValue("_id", BsonNull.Value);
            entity.OriginalId = ToString(originalId, 24);
            entity.Id = _options.GenerateNewUuids
                ? ConvertObjectIdToGuid(entity.OriginalId)
                : Guid.CreateVersion7();

            // Transform timestamps - handle multiple formats
            await TransformTimestamps(document, entity);

            // Transform glucose values - handle multiple formats (sgv, mgdl, mmol)
            TransformGlucoseValues(document, entity);

            // Transform direction with proper enum handling
            TransformDirection(document, entity);

            // Transform device and type information
            entity.Device = ToString(document.GetValue("device", BsonNull.Value), 255);
            entity.Type = ToString(document.GetValue("type", BsonNull.Value), 50) ?? "sgv";

            // Transform sensor data
            entity.Filtered = ToNullableDouble(document.GetValue("filtered", BsonNull.Value));
            entity.Unfiltered = ToNullableDouble(document.GetValue("unfiltered", BsonNull.Value));
            entity.Rssi = ToNullableInt32(document.GetValue("rssi", BsonNull.Value));
            entity.Noise = ToNullableInt32(document.GetValue("noise", BsonNull.Value));

            // Transform UTC offset
            entity.UtcOffset = ToNullableInt32(document.GetValue("utcOffset", BsonNull.Value));

            // Transform delta (change from previous reading)
            entity.Delta = ToNullableDouble(document.GetValue("delta", BsonNull.Value));

            // Transform metadata
            entity.CreatedAt = ConvertToDateTimeString(
                document.GetValue("created_at", BsonNull.Value)
            );
            entity.SysCreatedAt = DateTime.UtcNow;
            entity.SysUpdatedAt = DateTime.UtcNow;

            // Update statistics
            RecordTransformationSuccess();

            return entity;
        }
        catch (Exception ex)
        {
            RecordTransformationFailure(ex.Message);
            throw new InvalidOperationException(
                $"Failed to transform entry document: {ex.Message}",
                ex
            );
        }
    }

    public override async Task<TransformationValidationResult> ValidateAsync(BsonDocument document)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var suggestedFixes = new List<string>();

        await Task.CompletedTask; // Make async

        // Validate essential fields
        if (!document.Contains("_id"))
        {
            errors.Add("Document is missing required _id field");
            suggestedFixes.Add("Ensure all entry documents have a valid ObjectId");
        }

        // Validate glucose values - at least one should be present
        var hasSgv = document.Contains("sgv") && document["sgv"] != BsonNull.Value;
        var hasMgdl = document.Contains("mgdl") && document["mgdl"] != BsonNull.Value;
        var hasMmol = document.Contains("mmol") && document["mmol"] != BsonNull.Value;

        if (!hasSgv && !hasMgdl && !hasMmol)
        {
            warnings.Add("No glucose value found (sgv, mgdl, or mmol)");
            suggestedFixes.Add("Ensure entry documents contain at least one glucose reading");
        }

        // Validate timestamp fields
        var hasDate = document.Contains("date") && document["date"] != BsonNull.Value;
        var hasMills = document.Contains("mills") && document["mills"] != BsonNull.Value;
        var hasDateString =
            document.Contains("dateString") && document["dateString"] != BsonNull.Value;

        if (!hasDate && !hasMills && !hasDateString)
        {
            errors.Add("No valid timestamp found (date, mills, or dateString)");
            suggestedFixes.Add("Ensure entry documents contain valid timestamp information");
        }

        // Validate direction if present
        if (document.Contains("direction") && document["direction"] != BsonNull.Value)
        {
            var direction = document["direction"].ToString();
            if (!IsValidDirection(direction))
            {
                warnings.Add($"Invalid direction value: {direction}");
                suggestedFixes.Add(
                    "Use standard Nightscout direction values: Flat, SingleUp, DoubleUp, etc."
                );
            }
        }

        // Validate numeric ranges
        if (hasSgv)
        {
            var sgv = ToNullableDouble(document["sgv"]);
            if (sgv.HasValue && (sgv.Value < 0 || sgv.Value > 1000))
            {
                warnings.Add($"SGV value {sgv.Value} is outside normal range (0-1000 mg/dL)");
            }
        }

        // Validate delta if present
        if (document.Contains("delta") && document["delta"] != BsonNull.Value)
        {
            var delta = ToNullableDouble(document["delta"]);
            if (delta.HasValue && Math.Abs(delta.Value) > 100)
            {
                warnings.Add(
                    $"Delta value {delta.Value} seems unusually large (>100 mg/dL change)"
                );
            }
        }

        return new TransformationValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            SuggestedFixes = suggestedFixes,
        };
    }

    private async Task TransformTimestamps(BsonDocument document, EntryEntity entity)
    {
        await Task.CompletedTask; // Make async

        // Priority order: mills, date, dateString
        var mills = document.GetValue("mills", BsonNull.Value);
        var date = document.GetValue("date", BsonNull.Value);
        var dateString = document.GetValue("dateString", BsonNull.Value);

        if (mills != BsonNull.Value && mills.IsInt64)
        {
            entity.Mills = mills.AsInt64;
            UpdateFieldStatistics("mills", mills, true);
        }
        else if (date != BsonNull.Value)
        {
            var dateTime = ConvertToDateTime(date);
            entity.Mills = ((DateTimeOffset)dateTime).ToUnixTimeMilliseconds();
            UpdateFieldStatistics("date", date, true);
        }
        else if (dateString != BsonNull.Value && dateString.IsString)
        {
            var dateTime = ConvertToDateTime(dateString);
            entity.Mills = ((DateTimeOffset)dateTime).ToUnixTimeMilliseconds();
            UpdateFieldStatistics("dateString", dateString, true);
        }
        else
        {
            // Default to current time if no timestamp found
            entity.Mills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            RecordTransformationWarning();
        }

        // Store dateString for compatibility
        if (dateString != BsonNull.Value && dateString.IsString)
        {
            entity.DateString = ToString(dateString, 50);
        }
        else
        {
            // Generate dateString from mills
            entity.DateString = DateTimeOffset
                .FromUnixTimeMilliseconds(entity.Mills)
                .ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }
    }

    private void TransformGlucoseValues(BsonDocument document, EntryEntity entity)
    {
        // Handle multiple glucose value formats
        // Priority: sgv, mgdl, mmol (convert to mg/dL)

        var sgv = document.GetValue("sgv", BsonNull.Value);
        var mgdl = document.GetValue("mgdl", BsonNull.Value);
        var mmol = document.GetValue("mmol", BsonNull.Value);

        if (sgv != BsonNull.Value)
        {
            entity.Sgv = ToNullableDouble(sgv);
            UpdateFieldStatistics("sgv", sgv, true);
        }
        else if (mgdl != BsonNull.Value)
        {
            entity.Sgv = ToNullableDouble(mgdl);
            UpdateFieldStatistics("mgdl", mgdl, true);
        }
        else if (mmol != BsonNull.Value)
        {
            var mmolValue = ToNullableDouble(mmol);
            if (mmolValue.HasValue)
            {
                // Convert mmol/L to mg/dL (multiply by 18.0182)
                entity.Sgv = mmolValue.Value * 18.0182;
                UpdateFieldStatistics("mmol", mmol, true);
            }
        }
        else
        {
            UpdateMissingFieldStatistics("glucose_values");
        }
    }

    private void TransformDirection(BsonDocument document, EntryEntity entity)
    {
        var direction = document.GetValue("direction", BsonNull.Value);

        if (direction != BsonNull.Value)
        {
            var directionString = ToString(direction);

            // Store as object to handle both string and numeric values
            entity.Direction = NormalizeDirection(directionString);
            UpdateFieldStatistics("direction", direction, true);
        }
        else
        {
            entity.Direction = Direction.NONE.ToString();
            UpdateMissingFieldStatistics("direction");
        }
    }

    private bool IsValidDirection(string? direction)
    {
        if (string.IsNullOrEmpty(direction))
            return false;

        var validDirections = new[]
        {
            "NONE",
            "TripleUp",
            "DoubleUp",
            "SingleUp",
            "FortyFiveUp",
            "Flat",
            "FortyFiveDown",
            "SingleDown",
            "DoubleDown",
            "TripleDown",
            "NOT COMPUTABLE",
            "RATE OUT OF RANGE",
            "CGM ERROR",
        };

        return validDirections.Contains(direction, StringComparer.OrdinalIgnoreCase);
    }

    private string NormalizeDirection(string? direction)
    {
        if (string.IsNullOrEmpty(direction))
            return "NONE";

        // Handle legacy numeric direction values
        if (int.TryParse(direction, out var numericDirection))
        {
            return numericDirection switch
            {
                1 => "TripleUp",
                2 => "DoubleUp",
                3 => "SingleUp",
                4 => "FortyFiveUp",
                5 => "Flat",
                6 => "FortyFiveDown",
                7 => "SingleDown",
                8 => "DoubleDown",
                9 => "TripleDown",
                _ => "NONE",
            };
        }

        // Normalize string values
        return direction.ToUpperInvariant() switch
        {
            "NONE" or "0" => "NONE",
            "TRIPLEUP" or "TRIPLE UP" => "TripleUp",
            "DOUBLEUP" or "DOUBLE UP" => "DoubleUp",
            "SINGLEUP" or "SINGLE UP" => "SingleUp",
            "FORTYFIFEUP" or "FORTY FIVE UP" or "45UP" => "FortyFiveUp",
            "FLAT" => "Flat",
            "FORTYFIFEDOWN" or "FORTY FIVE DOWN" or "45DOWN" => "FortyFiveDown",
            "SINGLEDOWN" or "SINGLE DOWN" => "SingleDown",
            "DOUBLEDOWN" or "DOUBLE DOWN" => "DoubleDown",
            "TRIPLEDOWN" or "TRIPLE DOWN" => "TripleDown",
            "NOT COMPUTABLE" => "NOT COMPUTABLE",
            "RATE OUT OF RANGE" => "RATE OUT OF RANGE",
            "CGM ERROR" => "CGM ERROR",
            _ => direction, // Keep original if not recognized
        };
    }
}
