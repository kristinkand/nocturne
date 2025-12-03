using MongoDB.Bson;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Tools.Migration.Services.Transformers;

/// <summary>
/// Transformer for DeviceStatus documents
/// Handles nested device-specific data structures
/// </summary>
public class DeviceStatusTransformer : BaseDocumentTransformer
{
    public DeviceStatusTransformer(TransformationOptions? options = null)
        : base("devicestatus", options) { }

    public override async Task<object> TransformAsync(BsonDocument document)
    {
        try
        {
            var entity = new DeviceStatusEntity();

            // Transform ID
            var originalId = document.GetValue("_id", BsonNull.Value);
            entity.OriginalId = ToString(originalId, 24);
            entity.Id = _options.GenerateNewUuids
                ? ConvertObjectIdToGuid(entity.OriginalId)
                : Guid.CreateVersion7();

            // Transform basic fields
            entity.Device =
                ToString(document.GetValue("device", BsonNull.Value), 255) ?? string.Empty;
            entity.CreatedAt = ConvertToDateTimeString(
                document.GetValue("created_at", BsonNull.Value)
            );

            // Transform all additional properties to JSONB
            await TransformAdditionalProperties(document, entity);

            // Set system tracking timestamps
            entity.SysCreatedAt = DateTime.UtcNow;
            entity.SysUpdatedAt = DateTime.UtcNow;

            RecordTransformationSuccess();
            return entity;
        }
        catch (Exception ex)
        {
            RecordTransformationFailure(ex.Message);
            throw new InvalidOperationException(
                $"Failed to transform devicestatus document: {ex.Message}",
                ex
            );
        }
    }

    public override async Task<TransformationValidationResult> ValidateAsync(BsonDocument document)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var suggestedFixes = new List<string>();

        await Task.CompletedTask;

        if (!document.Contains("_id"))
        {
            errors.Add("Document is missing required _id field");
        }

        if (!document.Contains("device") || document["device"] == BsonNull.Value)
        {
            warnings.Add("DeviceStatus is missing device field");
        }

        return new TransformationValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            SuggestedFixes = suggestedFixes,
        };
    }

    private async Task TransformAdditionalProperties(
        BsonDocument document,
        DeviceStatusEntity entity
    )
    {
        await Task.CompletedTask;

        var standardFields = new HashSet<string> { "_id", "device", "created_at" };
        var additionalProps = new Dictionary<string, object?>();

        foreach (var element in document)
        {
            if (!standardFields.Contains(element.Name))
            {
                additionalProps[element.Name] = ConvertBsonValueToObject(element.Value);
            }
        }

        // Filter out null values if not preserving them
        var filteredProps = FilterNullProperties(additionalProps);

        if (filteredProps.Count > 0)
        {
            entity.AdditionalPropertiesJson = ToJsonB(BsonDocument.Create(filteredProps));
        }
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
}

/// <summary>
/// Transformer for Settings documents
/// Handles key-value pairs with JSONB values
/// </summary>
public class SettingsTransformer : BaseDocumentTransformer
{
    public SettingsTransformer(TransformationOptions? options = null)
        : base("settings", options) { }

    public override async Task<object> TransformAsync(BsonDocument document)
    {
        try
        {
            var entity = new SettingsEntity();

            var originalId = document.GetValue("_id", BsonNull.Value);
            entity.OriginalId = ToString(originalId, 24);
            entity.Id = _options.GenerateNewUuids
                ? ConvertObjectIdToGuid(entity.OriginalId)
                : Guid.CreateVersion7();

            entity.Key = ToString(document.GetValue("key", BsonNull.Value), 255) ?? string.Empty;
            entity.Value = ToJsonB(document.GetValue("value", BsonNull.Value));
            entity.CreatedAt = ConvertToDateTimeString(
                document.GetValue("created_at", BsonNull.Value)
            );

            // Set system tracking timestamps
            entity.SysCreatedAt = DateTime.UtcNow;
            entity.SysUpdatedAt = DateTime.UtcNow;

            RecordTransformationSuccess();
            return entity;
        }
        catch (Exception ex)
        {
            RecordTransformationFailure(ex.Message);
            throw new InvalidOperationException(
                $"Failed to transform settings document: {ex.Message}",
                ex
            );
        }
    }

    public override async Task<TransformationValidationResult> ValidateAsync(BsonDocument document)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        await Task.CompletedTask;

        if (!document.Contains("_id"))
        {
            errors.Add("Document is missing required _id field");
        }

        if (!document.Contains("key") || document["key"] == BsonNull.Value)
        {
            warnings.Add("Settings document is missing key field");
        }

        return new TransformationValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
        };
    }
}

/// <summary>
/// Transformer for Food documents
/// Handles QuickPick arrays and nutritional data
/// </summary>
public class FoodTransformer : BaseDocumentTransformer
{
    public FoodTransformer(TransformationOptions? options = null)
        : base("food", options) { }

    public override async Task<object> TransformAsync(BsonDocument document)
    {
        try
        {
            var entity = new FoodEntity();

            var originalId = document.GetValue("_id", BsonNull.Value);
            entity.OriginalId = ToString(originalId, 24);
            entity.Id = _options.GenerateNewUuids
                ? ConvertObjectIdToGuid(entity.OriginalId)
                : Guid.CreateVersion7();

            entity.Name = ToString(document.GetValue("name", BsonNull.Value), 255) ?? string.Empty;
            entity.Category =
                ToString(document.GetValue("category", BsonNull.Value), 255) ?? string.Empty;
            entity.Subcategory =
                ToString(document.GetValue("subcategory", BsonNull.Value), 255) ?? string.Empty;

            entity.Carbs = ConvertToRequiredDouble(
                ToNullableDouble(document.GetValue("carbs", BsonNull.Value))
            );
            entity.Protein = ConvertToRequiredDouble(
                ToNullableDouble(document.GetValue("protein", BsonNull.Value))
            );
            entity.Fat = ConvertToRequiredDouble(
                ToNullableDouble(document.GetValue("fat", BsonNull.Value))
            );
            entity.Energy = ConvertToRequiredDouble(
                ToNullableDouble(document.GetValue("energy", BsonNull.Value))
            );

            entity.SysCreatedAt = ConvertToDateTime(
                document.GetValue("created_at", BsonNull.Value)
            );
            entity.SysUpdatedAt = DateTime.UtcNow;

            RecordTransformationSuccess();
            return entity;
        }
        catch (Exception ex)
        {
            RecordTransformationFailure(ex.Message);
            throw new InvalidOperationException(
                $"Failed to transform food document: {ex.Message}",
                ex
            );
        }
    }

    public override async Task<TransformationValidationResult> ValidateAsync(BsonDocument document)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        await Task.CompletedTask;

        if (!document.Contains("_id"))
        {
            errors.Add("Document is missing required _id field");
        }

        if (!document.Contains("name") || document["name"] == BsonNull.Value)
        {
            warnings.Add("Food document is missing name field");
        }

        return new TransformationValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
        };
    }
}

/// <summary>
/// Transformer for Activity documents
/// Handles flexible additional properties
/// </summary>
public class ActivityTransformer : BaseDocumentTransformer
{
    public ActivityTransformer(TransformationOptions? options = null)
        : base("activity", options) { }

    public override async Task<object> TransformAsync(BsonDocument document)
    {
        try
        {
            var entity = new ActivityEntity();

            var originalId = document.GetValue("_id", BsonNull.Value);
            entity.OriginalId = ToString(originalId, 24);
            entity.Id = _options.GenerateNewUuids
                ? ConvertObjectIdToGuid(entity.OriginalId)
                : Guid.CreateVersion7();

            entity.Type = ToString(document.GetValue("activityType", BsonNull.Value), 100);
            entity.Description = ToString(document.GetValue("name", BsonNull.Value), 500);
            entity.Duration = ToNullableDouble(document.GetValue("duration", BsonNull.Value));
            entity.CreatedAt = ConvertToDateTimeString(
                document.GetValue("created_at", BsonNull.Value)
            );

            // Set system tracking timestamps
            entity.SysCreatedAt = DateTime.UtcNow;
            entity.SysUpdatedAt = DateTime.UtcNow;

            RecordTransformationSuccess();
            return entity;
        }
        catch (Exception ex)
        {
            RecordTransformationFailure(ex.Message);
            throw new InvalidOperationException(
                $"Failed to transform activity document: {ex.Message}",
                ex
            );
        }
    }

    public override async Task<TransformationValidationResult> ValidateAsync(BsonDocument document)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        await Task.CompletedTask;

        if (!document.Contains("_id"))
        {
            errors.Add("Document is missing required _id field");
        }

        return new TransformationValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
        };
    }
}

// NOTE: AuthTransformer commented out because main app doesn't have AuthEntity yet
/*
/// <summary>
/// Transformer for Auth documents
/// Handles roles arrays and permission structures
/// </summary>
public class AuthTransformer : BaseDocumentTransformer
{
    public AuthTransformer(TransformationOptions? options = null)
        : base("auth", options) { }

    public override async Task<object> TransformAsync(BsonDocument document)
    {
        try
        {
            var entity = new AuthEntity();

            var originalId = document.GetValue("_id", BsonNull.Value);
            entity.OriginalId = ToString(originalId, 24);
            entity.Id = _options.GenerateNewUuids
                ? ConvertObjectIdToGuid(entity.OriginalId)
                : Guid.CreateVersion7();

            entity.Username = ToString(document.GetValue("username", BsonNull.Value), 255);
            entity.Email = ToString(document.GetValue("email", BsonNull.Value), 255);
            entity.Password = ToString(document.GetValue("password", BsonNull.Value));
            entity.IsActive = document.GetValue("isActive", BsonBoolean.True).AsBoolean;

            entity.LastLogin =
                document.Contains("lastLogin") && document["lastLogin"] != BsonNull.Value
                    ? ConvertToDateTime(document["lastLogin"])
                    : null;

            entity.Created_at = ConvertToDateTime(document.GetValue("created_at", BsonNull.Value));

            // Transform roles array to JSONB
            await TransformRoles(document, entity);

            // Transform permissions to JSONB
            await TransformPermissions(document, entity);

            // Transform additional properties
            await TransformAdditionalProperties(document, entity);

            RecordTransformationSuccess();
            return entity;
        }
        catch (Exception ex)
        {
            RecordTransformationFailure(ex.Message);
            throw new InvalidOperationException(
                $"Failed to transform auth document: {ex.Message}",
                ex
            );
        }
    }

    public override async Task<TransformationValidationResult> ValidateAsync(BsonDocument document)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        await Task.CompletedTask;

        if (!document.Contains("_id"))
        {
            errors.Add("Document is missing required _id field");
        }

        if (!document.Contains("username") || document["username"] == BsonNull.Value)
        {
            warnings.Add("Auth document is missing username field");
        }

        return new TransformationValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
        };
    }

    /*
    private async Task TransformRoles(BsonDocument document, AuthEntity entity)
    {
        await Task.CompletedTask;

        var roles = document.GetValue("roles", BsonNull.Value);
        if (roles != BsonNull.Value)
        {
            entity.Roles = ToJsonB(roles);
        }
    }

    private async Task TransformPermissions(BsonDocument document, AuthEntity entity)
    {
        await Task.CompletedTask;

        var permissions = document.GetValue("permissions", BsonNull.Value);
        if (permissions != BsonNull.Value)
        {
            entity.Permissions = ToJsonB(permissions);
        }
    }
    */

/*
private async Task TransformAdditionalProperties(BsonDocument document, AuthEntity entity)
{
    await Task.CompletedTask;

    var standardFields = new HashSet<string>
    {
        "_id",
        "username",
        "email",
        "password",
        "isActive",
        "lastLogin",
        "created_at",
        "roles",
        "permissions",
    };

    var additionalProps = new Dictionary<string, object?>();

    foreach (var element in document)
    {
        if (!standardFields.Contains(element.Name))
        {
            additionalProps[element.Name] = ConvertBsonValueToObject(element.Value);
        }
    }

    // Filter out null values if not preserving them
    var filteredProps = FilterNullProperties(additionalProps);

    if (filteredProps.Count > 0)
    {
        entity.AdditionalProperties = ToJsonB(BsonDocument.Create(filteredProps));
    }
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
}
*/
