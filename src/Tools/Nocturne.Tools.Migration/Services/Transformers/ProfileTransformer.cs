using MongoDB.Bson;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Tools.Migration.Services.Transformers;

/// <summary>
/// Transformer for Profile documents
/// Handles time-based values and profile store normalization
/// </summary>
public class ProfileTransformer : BaseDocumentTransformer
{
    public ProfileTransformer(TransformationOptions? options = null)
        : base("profiles", options) { }

    public override async Task<object> TransformAsync(BsonDocument document)
    {
        try
        {
            var entity = new ProfileEntity();

            // Transform ID
            var originalId = document.GetValue("_id", BsonNull.Value);
            entity.OriginalId = ToString(originalId, 24);
            entity.Id = _options.GenerateNewUuids
                ? ConvertObjectIdToGuid(entity.OriginalId)
                : Guid.CreateVersion7();

            // Transform basic profile information
            entity.DefaultProfile =
                ToString(document.GetValue("defaultProfile", BsonNull.Value), 255) ?? "Default";
            entity.Units = ToString(document.GetValue("units", BsonNull.Value), 10) ?? "mg/dL";

            // Transform timestamps
            await TransformTimestamps(document, entity);

            // Transform the complex profile store to normalized JSONB
            await TransformProfileStore(document, entity);

            // Transform created timestamp
            entity.CreatedAt = ConvertToDateTimeString(
                document.GetValue("created_at", BsonNull.Value)
            );

            // Update statistics
            RecordTransformationSuccess();

            return entity;
        }
        catch (Exception ex)
        {
            RecordTransformationFailure(ex.Message);
            throw new InvalidOperationException(
                $"Failed to transform profile document: {ex.Message}",
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
            suggestedFixes.Add("Ensure all profile documents have a valid ObjectId");
        }

        // Validate profile store
        if (!document.Contains("store") || document["store"] == BsonNull.Value)
        {
            errors.Add("Profile is missing store field");
            suggestedFixes.Add("Ensure profile contains a valid store object");
        }
        else
        {
            await ValidateProfileStore(document["store"], errors, warnings, suggestedFixes);
        }

        // Validate defaultProfile
        if (!document.Contains("defaultProfile") || document["defaultProfile"] == BsonNull.Value)
        {
            warnings.Add("Profile is missing defaultProfile field");
            suggestedFixes.Add("Specify defaultProfile to indicate which profile is active");
        }

        // Validate units
        if (document.Contains("units") && document["units"] != BsonNull.Value)
        {
            var units = document["units"].ToString();
            if (!IsValidUnits(units))
            {
                warnings.Add($"Invalid units value: {units}");
                suggestedFixes.Add("Use 'mg/dL' or 'mmol/L' for units");
            }
        }

        // Validate timestamp fields
        var hasStartDate =
            document.Contains("startDate") && document["startDate"] != BsonNull.Value;
        var hasCreatedAt =
            document.Contains("created_at") && document["created_at"] != BsonNull.Value;
        var hasMills = document.Contains("mills") && document["mills"] != BsonNull.Value;

        if (!hasStartDate && !hasCreatedAt && !hasMills)
        {
            warnings.Add("No valid timestamp found");
            suggestedFixes.Add("Include startDate or created_at for proper profile timing");
        }

        return new TransformationValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            SuggestedFixes = suggestedFixes,
        };
    }

    private async Task TransformTimestamps(BsonDocument document, ProfileEntity entity)
    {
        await Task.CompletedTask; // Make async

        // Priority: startDate, created_at, mills
        var startDate = document.GetValue("startDate", BsonNull.Value);
        var createdAt = document.GetValue("created_at", BsonNull.Value);
        var mills = document.GetValue("mills", BsonNull.Value);

        if (startDate != BsonNull.Value)
        {
            entity.StartDate = ConvertToDateTimeString(startDate);
            UpdateFieldStatistics("startDate", startDate, true);
        }
        else if (createdAt != BsonNull.Value)
        {
            entity.StartDate = ConvertToDateTimeString(createdAt);
            UpdateFieldStatistics("created_at", createdAt, true);
        }
        else if (mills != BsonNull.Value && mills.IsInt64)
        {
            entity.StartDate = DateTimeOffset
                .FromUnixTimeMilliseconds(mills.AsInt64)
                .ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            UpdateFieldStatistics("mills", mills, true);
        }
        else
        {
            entity.StartDate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            RecordTransformationWarning();
        }
    }

    private async Task TransformProfileStore(BsonDocument document, ProfileEntity entity)
    {
        await Task.CompletedTask; // Make async

        var store = document.GetValue("store", BsonNull.Value);

        if (store != BsonNull.Value && store.IsBsonDocument)
        {
            try
            {
                var storeDoc = store.AsBsonDocument;
                var normalizedStore = new Dictionary<string, object>();

                foreach (var profileElement in storeDoc)
                {
                    var profileName = profileElement.Name;
                    var profileData = profileElement.Value;

                    if (profileData.IsBsonDocument)
                    {
                        var normalizedProfile = await NormalizeProfileData(
                            profileData.AsBsonDocument
                        );
                        normalizedStore[profileName] = normalizedProfile;
                    }
                    else
                    {
                        // Handle non-document profile data
                        normalizedStore[profileName] =
                            ConvertBsonValueToObject(profileData) ?? new object();
                    }
                }

                entity.StoreJson = ToJsonB(BsonDocument.Create(normalizedStore));
                UpdateFieldStatistics("store", store, true);
            }
            catch (Exception ex)
            {
                UpdateFieldStatistics("store", store, false);
                RecordTransformationFailure($"Profile store transformation failed: {ex.Message}");
            }
        }
        else
        {
            UpdateMissingFieldStatistics("store");
        }
    }

    private async Task<Dictionary<string, object>> NormalizeProfileData(BsonDocument profileData)
    {
        await Task.CompletedTask; // Make async

        var normalized = new Dictionary<string, object>();

        // Transform basic profile settings
        if (profileData.Contains("dia"))
        {
            normalized["dia"] = ToNullableDouble(profileData["dia"]) ?? 3.0;
        }

        if (profileData.Contains("carbs_hr"))
        {
            normalized["carbs_hr"] = ToNullableInt32(profileData["carbs_hr"]) ?? 20;
        }

        if (profileData.Contains("delay"))
        {
            normalized["delay"] = ToNullableInt32(profileData["delay"]) ?? 20;
        }

        if (profileData.Contains("timezone"))
        {
            normalized["timezone"] = ToString(profileData["timezone"]) ?? "UTC";
        }

        // Transform time-based arrays (basal, carbratio, sens, target_low, target_high)
        await TransformTimeBasedArray(profileData, normalized, "basal");
        await TransformTimeBasedArray(profileData, normalized, "carbratio");
        await TransformTimeBasedArray(profileData, normalized, "sens");
        await TransformTimeBasedArray(profileData, normalized, "target_low");
        await TransformTimeBasedArray(profileData, normalized, "target_high");

        // Handle any additional fields
        foreach (var element in profileData)
        {
            if (!normalized.ContainsKey(element.Name))
            {
                normalized[element.Name] = ConvertBsonValueToObject(element.Value) ?? new object();
            }
        }

        return normalized;
    }

    private async Task TransformTimeBasedArray(
        BsonDocument profileData,
        Dictionary<string, object> normalized,
        string fieldName
    )
    {
        await Task.CompletedTask; // Make async

        if (!profileData.Contains(fieldName))
            return;

        var fieldValue = profileData[fieldName];

        if (fieldValue.IsBsonArray)
        {
            // Transform array of time-value pairs
            var timeBasedArray = fieldValue.AsBsonArray;
            var normalizedArray = new List<Dictionary<string, object>>();

            foreach (var item in timeBasedArray)
            {
                if (item.IsBsonDocument)
                {
                    var itemDoc = item.AsBsonDocument;
                    var normalizedItem = new Dictionary<string, object>();

                    // Normalize time field (can be "time", "timeAsSeconds", or other formats)
                    if (itemDoc.Contains("time"))
                    {
                        normalizedItem["time"] = NormalizeTimeString(ToString(itemDoc["time"]));
                    }
                    else if (itemDoc.Contains("timeAsSeconds"))
                    {
                        var seconds = ToNullableInt32(itemDoc["timeAsSeconds"]);
                        if (seconds.HasValue)
                        {
                            normalizedItem["time"] = SecondsToTimeString(seconds.Value);
                        }
                    }

                    // Normalize value field
                    if (itemDoc.Contains("value"))
                    {
                        normalizedItem["value"] = ToNullableDouble(itemDoc["value"]);
                    }

                    // Copy any additional fields
                    foreach (var element in itemDoc)
                    {
                        if (
                            element.Name != "time"
                            && element.Name != "timeAsSeconds"
                            && element.Name != "value"
                        )
                        {
                            normalizedItem[element.Name] =
                                ConvertBsonValueToObject(element.Value) ?? new object();
                        }
                    }

                    normalizedArray.Add(normalizedItem);
                }
            }

            normalized[fieldName] = normalizedArray;
        }
        else
        {
            // Handle non-array values
            normalized[fieldName] = ConvertBsonValueToObject(fieldValue) ?? new object();
        }
    }

    private string NormalizeTimeString(string? timeString)
    {
        if (string.IsNullOrEmpty(timeString))
            return "00:00";

        // Handle various time formats
        if (TimeSpan.TryParse(timeString, out var timeSpan))
        {
            return timeSpan.ToString(@"hh\:mm");
        }

        // Handle "HH:mm" format
        if (timeString.Contains(':') && timeString.Length <= 5)
        {
            return timeString;
        }

        // Handle seconds as string
        if (int.TryParse(timeString, out var seconds))
        {
            return SecondsToTimeString(seconds);
        }

        return timeString; // Return as-is if can't parse
    }

    private string SecondsToTimeString(int seconds)
    {
        var hours = seconds / 3600;
        var minutes = (seconds % 3600) / 60;
        return $"{hours:D2}:{minutes:D2}";
    }

    private object? ConvertBsonValueToObject(BsonValue value)
    {
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
            BsonType.Array => value.AsBsonArray.Select(ConvertBsonValueToObject).ToArray(),
            BsonType.Document => value.AsBsonDocument.ToDictionary(
                element => element.Name,
                element => ConvertBsonValueToObject(element.Value)
            ),
            _ => value.ToString(),
        };
    }

    private async Task ValidateProfileStore(
        BsonValue store,
        List<string> errors,
        List<string> warnings,
        List<string> suggestedFixes
    )
    {
        await Task.CompletedTask; // Make async

        if (!store.IsBsonDocument)
        {
            errors.Add("Profile store is not a valid document structure");
            suggestedFixes.Add("Ensure profile store is properly structured as an object");
            return;
        }

        var storeDoc = store.AsBsonDocument;

        if (storeDoc.ElementCount == 0)
        {
            errors.Add("Profile store is empty");
            suggestedFixes.Add("Profile store must contain at least one profile");
            return;
        }

        foreach (var profileElement in storeDoc)
        {
            var profileName = profileElement.Name;
            var profileData = profileElement.Value;

            if (!profileData.IsBsonDocument)
            {
                warnings.Add($"Profile '{profileName}' is not a valid document structure");
                continue;
            }

            var profileDoc = profileData.AsBsonDocument;
            await ValidateIndividualProfile(profileName, profileDoc, warnings, suggestedFixes);
        }
    }

    private async Task ValidateIndividualProfile(
        string profileName,
        BsonDocument profileDoc,
        List<string> warnings,
        List<string> suggestedFixes
    )
    {
        await Task.CompletedTask; // Make async

        // Check for essential profile fields
        var requiredFields = new[] { "dia", "carbratio", "sens", "basal" };

        foreach (var field in requiredFields)
        {
            if (!profileDoc.Contains(field) || profileDoc[field] == BsonNull.Value)
            {
                warnings.Add($"Profile '{profileName}' is missing required field: {field}");
                suggestedFixes.Add($"Add {field} field to profile '{profileName}'");
            }
        }

        // Validate DIA value
        if (profileDoc.Contains("dia"))
        {
            var dia = ToNullableDouble(profileDoc["dia"]);
            if (dia.HasValue && (dia.Value < 1 || dia.Value > 10))
            {
                warnings.Add($"Profile '{profileName}' has unusual DIA value: {dia.Value}");
                suggestedFixes.Add("DIA should typically be between 1-10 hours");
            }
        }

        // Validate time-based arrays
        await ValidateTimeBasedArray(profileName, profileDoc, "basal", warnings, suggestedFixes);
        await ValidateTimeBasedArray(
            profileName,
            profileDoc,
            "carbratio",
            warnings,
            suggestedFixes
        );
        await ValidateTimeBasedArray(profileName, profileDoc, "sens", warnings, suggestedFixes);
    }

    private async Task ValidateTimeBasedArray(
        string profileName,
        BsonDocument profileDoc,
        string fieldName,
        List<string> warnings,
        List<string> suggestedFixes
    )
    {
        await Task.CompletedTask; // Make async

        if (!profileDoc.Contains(fieldName))
            return;

        var fieldValue = profileDoc[fieldName];

        if (!fieldValue.IsBsonArray)
        {
            warnings.Add($"Profile '{profileName}' field '{fieldName}' should be an array");
            return;
        }

        var array = fieldValue.AsBsonArray;

        if (array.Count == 0)
        {
            warnings.Add($"Profile '{profileName}' field '{fieldName}' is empty");
            suggestedFixes.Add($"Add at least one entry to {fieldName} array");
            return;
        }

        // Validate array structure
        foreach (var item in array)
        {
            if (!item.IsBsonDocument)
            {
                warnings.Add($"Profile '{profileName}' field '{fieldName}' contains invalid entry");
                continue;
            }

            var itemDoc = item.AsBsonDocument;

            // Should have time and value
            var hasTime = itemDoc.Contains("time") || itemDoc.Contains("timeAsSeconds");
            var hasValue = itemDoc.Contains("value");

            if (!hasTime)
            {
                warnings.Add(
                    $"Profile '{profileName}' field '{fieldName}' entry missing time field"
                );
            }

            if (!hasValue)
            {
                warnings.Add(
                    $"Profile '{profileName}' field '{fieldName}' entry missing value field"
                );
            }
        }
    }

    private bool IsValidUnits(string? units)
    {
        if (string.IsNullOrEmpty(units))
            return false;

        var validUnits = new[] { "mg/dL", "mg/dl", "mmol/L", "mmol/l", "mmol" };
        return validUnits.Contains(units, StringComparer.OrdinalIgnoreCase);
    }
}
