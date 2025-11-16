using System.Text.Json;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Common;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Infrastructure.Data.Mappers;

/// <summary>
/// Mapper for converting between Activity domain models and ActivityEntity database entities
/// </summary>
public static class ActivityMapper
{
    /// <summary>
    /// Convert domain model to database entity
    /// </summary>
    public static ActivityEntity ToEntity(Activity activity)
    {
        return new ActivityEntity
        {
            Id = string.IsNullOrEmpty(activity.Id)
                ? Guid.CreateVersion7()
                : ParseIdToGuid(activity.Id),
            OriginalId = MongoIdUtils.IsValidMongoId(activity.Id) ? activity.Id : null,
            Mills = activity.Mills,
            DateString = activity.DateString,
            Type = activity.Type,
            Description = activity.Description,
            Duration = activity.Duration,
            Intensity = activity.Intensity,
            Notes = activity.Notes,
            EnteredBy = activity.EnteredBy,
            UtcOffset = activity.UtcOffset,
            Timestamp = activity.Timestamp,
            CreatedAt = activity.CreatedAt,
            AdditionalPropertiesJson =
                activity.AdditionalProperties != null
                    ? JsonSerializer.Serialize(activity.AdditionalProperties)
                    : null,
        };
    }

    /// <summary>
    /// Convert database entity to domain model
    /// </summary>
    public static Activity ToDomainModel(ActivityEntity entity)
    {
        return new Activity
        {
            Id = entity.OriginalId ?? entity.Id.ToString(),
            Mills = entity.Mills,
            DateString = entity.DateString,
            Type = entity.Type,
            Description = entity.Description,
            Duration = entity.Duration,
            Intensity = entity.Intensity,
            Notes = entity.Notes,
            EnteredBy = entity.EnteredBy,
            UtcOffset = entity.UtcOffset,
            Timestamp = entity.Timestamp,
            CreatedAt = entity.CreatedAt,
            AdditionalProperties = DeserializeJsonProperty<Dictionary<string, object>>(
                entity.AdditionalPropertiesJson
            ),
        };
    }

    /// <summary>
    /// Update existing entity with data from domain model
    /// </summary>
    public static void UpdateEntity(ActivityEntity entity, Activity activity)
    {
        entity.Mills = activity.Mills;
        entity.DateString = activity.DateString;
        entity.Type = activity.Type;
        entity.Description = activity.Description;
        entity.Duration = activity.Duration;
        entity.Intensity = activity.Intensity;
        entity.Notes = activity.Notes;
        entity.EnteredBy = activity.EnteredBy;
        entity.UtcOffset = activity.UtcOffset;
        entity.Timestamp = activity.Timestamp;
        entity.CreatedAt = activity.CreatedAt;
        entity.AdditionalPropertiesJson =
            activity.AdditionalProperties != null
                ? JsonSerializer.Serialize(activity.AdditionalProperties)
                : null;
        entity.SysUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Parse string ID to GUID, or generate new GUID if invalid
    /// </summary>
    private static Guid ParseIdToGuid(string id)
    {
        // Hash the ID to get a deterministic GUID for consistent mapping
        // This ensures the same string ID always maps to the same GUID
        if (string.IsNullOrEmpty(id))
        {
            return Guid.CreateVersion7();
        }

        // Use a simple hash approach for deterministic GUID generation
        var hash = System.Security.Cryptography.SHA1.HashData(
            System.Text.Encoding.UTF8.GetBytes(id)
        );
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);
        return new Guid(guidBytes);
    }

    /// <summary>
    /// Deserialize JSON property safely
    /// </summary>
    private static T? DeserializeJsonProperty<T>(string? json)
        where T : class
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            // Return null if deserialization fails
            return null;
        }
    }
}
