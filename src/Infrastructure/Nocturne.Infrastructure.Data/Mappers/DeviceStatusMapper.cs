using System.Text.Json;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Common;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Infrastructure.Data.Mappers;

/// <summary>
/// Mapper for converting between DeviceStatus domain models and DeviceStatusEntity database entities
/// </summary>
public static class DeviceStatusMapper
{
    /// <summary>
    /// Convert domain model to database entity
    /// </summary>
    public static DeviceStatusEntity ToEntity(DeviceStatus deviceStatus)
    {
        return new DeviceStatusEntity
        {
            Id = string.IsNullOrEmpty(deviceStatus.Id)
                ? Guid.CreateVersion7()
                : ParseIdToGuid(deviceStatus.Id),
            OriginalId = MongoIdUtils.IsValidMongoId(deviceStatus.Id) ? deviceStatus.Id : null,
            Mills = deviceStatus.Mills,
            CreatedAt = deviceStatus.CreatedAt,
            UtcOffset = deviceStatus.UtcOffset,
            Device = deviceStatus.Device,
            IsCharging = deviceStatus.IsCharging,
            UploaderJson =
                deviceStatus.Uploader != null
                    ? JsonSerializer.Serialize(deviceStatus.Uploader)
                    : null,
            PumpJson =
                deviceStatus.Pump != null ? JsonSerializer.Serialize(deviceStatus.Pump) : null,
            OpenApsJson =
                deviceStatus.OpenAps != null
                    ? JsonSerializer.Serialize(deviceStatus.OpenAps)
                    : null,
            LoopJson =
                deviceStatus.Loop != null ? JsonSerializer.Serialize(deviceStatus.Loop) : null,
            XDripJsJson =
                deviceStatus.XDripJs != null
                    ? JsonSerializer.Serialize(deviceStatus.XDripJs)
                    : null,
            RadioAdapterJson =
                deviceStatus.RadioAdapter != null
                    ? JsonSerializer.Serialize(deviceStatus.RadioAdapter)
                    : null,
            ConnectJson =
                deviceStatus.Connect != null
                    ? JsonSerializer.Serialize(deviceStatus.Connect)
                    : null,
            OverrideJson =
                deviceStatus.Override != null
                    ? JsonSerializer.Serialize(deviceStatus.Override)
                    : null,
            CgmJson = deviceStatus.Cgm != null ? JsonSerializer.Serialize(deviceStatus.Cgm) : null,
            MeterJson =
                deviceStatus.Meter != null ? JsonSerializer.Serialize(deviceStatus.Meter) : null,
            InsulinPenJson =
                deviceStatus.InsulinPen != null
                    ? JsonSerializer.Serialize(deviceStatus.InsulinPen)
                    : null,
        };
    }

    /// <summary>
    /// Convert database entity to domain model
    /// </summary>
    public static DeviceStatus ToDomainModel(DeviceStatusEntity entity)
    {
        return new DeviceStatus
        {
            Id = entity.OriginalId ?? entity.Id.ToString(),
            Mills = entity.Mills,
            CreatedAt = entity.CreatedAt,
            UtcOffset = entity.UtcOffset,
            Device = entity.Device,
            IsCharging = entity.IsCharging,
            Uploader = DeserializeJsonProperty<UploaderStatus>(entity.UploaderJson),
            Pump = DeserializeJsonProperty<PumpStatus>(entity.PumpJson),
            OpenAps = DeserializeJsonProperty<OpenApsStatus>(entity.OpenApsJson),
            Loop = DeserializeJsonProperty<LoopStatus>(entity.LoopJson),
            XDripJs = DeserializeJsonProperty<XDripJsStatus>(entity.XDripJsJson),
            RadioAdapter = DeserializeJsonProperty<RadioAdapterStatus>(entity.RadioAdapterJson),
            Connect = DeserializeJsonProperty<object>(entity.ConnectJson),
            Override = DeserializeJsonProperty<OverrideStatus>(entity.OverrideJson),
            Cgm = DeserializeJsonProperty<CgmStatus>(entity.CgmJson),
            Meter = DeserializeJsonProperty<MeterStatus>(entity.MeterJson),
            InsulinPen = DeserializeJsonProperty<InsulinPenStatus>(entity.InsulinPenJson),
        };
    }

    /// <summary>
    /// Update existing entity with data from domain model
    /// </summary>
    public static void UpdateEntity(DeviceStatusEntity entity, DeviceStatus deviceStatus)
    {
        entity.Mills = deviceStatus.Mills;
        entity.CreatedAt = deviceStatus.CreatedAt;
        entity.UtcOffset = deviceStatus.UtcOffset;
        entity.Device = deviceStatus.Device;
        entity.IsCharging = deviceStatus.IsCharging;
        entity.UploaderJson =
            deviceStatus.Uploader != null ? JsonSerializer.Serialize(deviceStatus.Uploader) : null;
        entity.PumpJson =
            deviceStatus.Pump != null ? JsonSerializer.Serialize(deviceStatus.Pump) : null;
        entity.OpenApsJson =
            deviceStatus.OpenAps != null ? JsonSerializer.Serialize(deviceStatus.OpenAps) : null;
        entity.LoopJson =
            deviceStatus.Loop != null ? JsonSerializer.Serialize(deviceStatus.Loop) : null;
        entity.XDripJsJson =
            deviceStatus.XDripJs != null ? JsonSerializer.Serialize(deviceStatus.XDripJs) : null;
        entity.RadioAdapterJson =
            deviceStatus.RadioAdapter != null
                ? JsonSerializer.Serialize(deviceStatus.RadioAdapter)
                : null;
        entity.ConnectJson =
            deviceStatus.Connect != null ? JsonSerializer.Serialize(deviceStatus.Connect) : null;
        entity.OverrideJson =
            deviceStatus.Override != null ? JsonSerializer.Serialize(deviceStatus.Override) : null;
        entity.CgmJson =
            deviceStatus.Cgm != null ? JsonSerializer.Serialize(deviceStatus.Cgm) : null;
        entity.MeterJson =
            deviceStatus.Meter != null ? JsonSerializer.Serialize(deviceStatus.Meter) : null;
        entity.InsulinPenJson =
            deviceStatus.InsulinPen != null
                ? JsonSerializer.Serialize(deviceStatus.InsulinPen)
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
            return Guid.CreateVersion7();

        try
        {
            // Use a simple hash of the ID to generate a consistent GUID
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            var hashBytes = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(id));
            var guidBytes = new byte[16];
            Array.Copy(hashBytes, guidBytes, 16);
            return new Guid(guidBytes);
        }
        catch
        {
            // If anything fails, generate a new GUID
            return Guid.CreateVersion7();
        }
    }

    /// <summary>
    /// Safely deserialize JSON property
    /// </summary>
    private static T? DeserializeJsonProperty<T>(string? json)
    {
        if (string.IsNullOrEmpty(json) || json == "null")
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }
}
