using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services;

/// <summary>
/// Service for device registry and management operations
/// </summary>
public class DeviceRegistryService : IDeviceRegistryService
{
    private readonly NocturneDbContext _dbContext;
    private readonly ILogger<DeviceRegistryService> _logger;
    private readonly DeviceHealthOptions _options;

    /// <summary>
    /// Initializes a new instance of the DeviceRegistryService
    /// </summary>
    /// <param name="dbContext">Database context</param>
    /// <param name="logger">Logger</param>
    /// <param name="options">Device health options</param>
    public DeviceRegistryService(
        NocturneDbContext dbContext,
        ILogger<DeviceRegistryService> logger,
        IOptions<DeviceHealthOptions> options
    )
    {
        _dbContext = dbContext;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Register a new device for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="request">Device registration request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Registered device health entity</returns>
    public async Task<DeviceHealth> RegisterDeviceAsync(
        string userId,
        DeviceRegistrationRequest request,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

        if (string.IsNullOrWhiteSpace(request.DeviceId))
            throw new ArgumentException(
                "Device ID cannot be null or empty",
                nameof(request.DeviceId)
            );

        _logger.LogInformation(
            "Registering device {DeviceId} for user {UserId}",
            request.DeviceId,
            userId
        );

        // Check if user has reached maximum device limit
        var userDeviceCount = await _dbContext
            .DeviceHealth.Where(d => d.UserId == userId)
            .CountAsync(cancellationToken);

        if (userDeviceCount >= _options.MaxDevicesPerUser)
        {
            throw new InvalidOperationException(
                $"User has reached maximum device limit of {_options.MaxDevicesPerUser}"
            );
        }

        // Check if device already exists
        var existingDevice = await _dbContext.DeviceHealth.FirstOrDefaultAsync(
            d => d.DeviceId == request.DeviceId,
            cancellationToken
        );

        if (existingDevice != null)
        {
            throw new InvalidOperationException($"Device {request.DeviceId} is already registered");
        }

        // Create new device health entity
        var deviceHealthEntity = new DeviceHealthEntity
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            DeviceId = request.DeviceId,
            DeviceType = request.DeviceType,
            DeviceName = request.DeviceName,
            Manufacturer = request.Manufacturer,
            Model = request.Model,
            SerialNumber = request.SerialNumber,
            BatteryLevel = request.BatteryLevel,
            SensorExpiration = request.SensorExpiration,
            Status = DeviceStatusType.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _dbContext.DeviceHealth.Add(deviceHealthEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Successfully registered device {DeviceId} for user {UserId}",
            request.DeviceId,
            userId
        );

        return MapToDto(deviceHealthEntity);
    }

    /// <summary>
    /// Get all devices for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of user's devices</returns>
    public async Task<List<DeviceHealth>> GetUserDevicesAsync(
        string userId,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

        _logger.LogDebug("Getting devices for user {UserId}", userId);

        var devices = await _dbContext
            .DeviceHealth.Where(d => d.UserId == userId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        return devices.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Update device health metrics
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="update">Device health update data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task completion</returns>
    public async Task UpdateDeviceHealthAsync(
        string deviceId,
        DeviceHealthUpdate update,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("Device ID cannot be null or empty", nameof(deviceId));

        _logger.LogDebug("Updating device health for device {DeviceId}", deviceId);

        var device = await _dbContext.DeviceHealth.FirstOrDefaultAsync(
            d => d.DeviceId == deviceId,
            cancellationToken
        );

        if (device == null)
        {
            throw new InvalidOperationException($"Device {deviceId} not found");
        }

        // Update device health metrics
        if (update.BatteryLevel.HasValue)
            device.BatteryLevel = update.BatteryLevel.Value;

        if (update.SensorExpiration.HasValue)
            device.SensorExpiration = update.SensorExpiration.Value;

        if (update.LastCalibration.HasValue)
            device.LastCalibration = update.LastCalibration.Value;

        if (update.Status.HasValue)
            device.Status = update.Status.Value;

        if (!string.IsNullOrWhiteSpace(update.LastErrorMessage))
            device.LastErrorMessage = update.LastErrorMessage;

        device.LastDataReceived = DateTime.UtcNow;
        device.LastStatusUpdate = DateTime.UtcNow;
        device.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Successfully updated device health for device {DeviceId}", deviceId);
    }

    /// <summary>
    /// Get device health information
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Device health entity or null if not found</returns>
    public async Task<DeviceHealth?> GetDeviceAsync(
        string deviceId,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("Device ID cannot be null or empty", nameof(deviceId));

        _logger.LogDebug("Getting device {DeviceId}", deviceId);

        var device = await _dbContext.DeviceHealth.FirstOrDefaultAsync(
            d => d.DeviceId == deviceId,
            cancellationToken
        );

        return device != null ? MapToDto(device) : null;
    }

    /// <summary>
    /// Remove a device from the registry
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task completion</returns>
    public async Task RemoveDeviceAsync(string deviceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("Device ID cannot be null or empty", nameof(deviceId));

        _logger.LogInformation("Removing device {DeviceId}", deviceId);

        var device = await _dbContext.DeviceHealth.FirstOrDefaultAsync(
            d => d.DeviceId == deviceId,
            cancellationToken
        );

        if (device == null)
        {
            throw new InvalidOperationException($"Device {deviceId} not found");
        }

        _dbContext.DeviceHealth.Remove(device);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully removed device {DeviceId}", deviceId);
    }

    /// <summary>
    /// Update device settings
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="settings">Device settings update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task completion</returns>
    public async Task UpdateDeviceSettingsAsync(
        string deviceId,
        DeviceSettingsUpdate settings,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("Device ID cannot be null or empty", nameof(deviceId));

        _logger.LogDebug("Updating device settings for device {DeviceId}", deviceId);

        var device = await _dbContext.DeviceHealth.FirstOrDefaultAsync(
            d => d.DeviceId == deviceId,
            cancellationToken
        );

        if (device == null)
        {
            throw new InvalidOperationException($"Device {deviceId} not found");
        }

        // Update device settings
        if (settings.BatteryWarningThreshold.HasValue)
            device.BatteryWarningThreshold = settings.BatteryWarningThreshold.Value;

        if (settings.SensorExpirationWarningHours.HasValue)
            device.SensorExpirationWarningHours = settings.SensorExpirationWarningHours.Value;

        if (settings.DataGapWarningMinutes.HasValue)
            device.DataGapWarningMinutes = settings.DataGapWarningMinutes.Value;

        if (settings.CalibrationReminderHours.HasValue)
            device.CalibrationReminderHours = settings.CalibrationReminderHours.Value;

        device.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Successfully updated device settings for device {DeviceId}", deviceId);
    }

    /// <summary>
    /// Map DeviceHealthEntity to DeviceHealth DTO
    /// </summary>
    /// <param name="entity">Device health entity</param>
    /// <returns>Device health DTO</returns>
    private static DeviceHealth MapToDto(DeviceHealthEntity entity)
    {
        return new DeviceHealth
        {
            Id = entity.Id,
            UserId = entity.UserId,
            DeviceId = entity.DeviceId,
            DeviceType = entity.DeviceType,
            DeviceName = entity.DeviceName,
            Manufacturer = entity.Manufacturer,
            Model = entity.Model,
            SerialNumber = entity.SerialNumber,
            BatteryLevel = entity.BatteryLevel,
            SensorExpiration = entity.SensorExpiration,
            LastCalibration = entity.LastCalibration,
            LastDataReceived = entity.LastDataReceived,
            LastMaintenanceAlert = entity.LastMaintenanceAlert,
            BatteryWarningThreshold = entity.BatteryWarningThreshold,
            SensorExpirationWarningHours = entity.SensorExpirationWarningHours,
            DataGapWarningMinutes = entity.DataGapWarningMinutes,
            CalibrationReminderHours = entity.CalibrationReminderHours,
            Status = entity.Status,
            LastErrorMessage = entity.LastErrorMessage,
            LastStatusUpdate = entity.LastStatusUpdate,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }
}
