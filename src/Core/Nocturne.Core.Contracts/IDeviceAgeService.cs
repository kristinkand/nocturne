using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts;

/// <summary>
/// Base interface for device age tracking services with 1:1 legacy JavaScript compatibility
/// </summary>
public interface IDeviceAgeService
{
    /// <summary>
    /// Calculate age information for a specific device type based on treatments
    /// </summary>
    /// <param name="treatments">List of treatments to analyze</param>
    /// <param name="currentTime">Current time in milliseconds since Unix epoch</param>
    /// <param name="preferences">Device-specific preferences</param>
    /// <returns>Device age information</returns>
    DeviceAgeInfo CalculateDeviceAge(
        List<Treatment> treatments,
        long currentTime,
        DeviceAgePreferences preferences
    );
}

/// <summary>
/// Interface for cannula age tracking service
/// </summary>
public interface ICannulaAgeService : IDeviceAgeService { }

/// <summary>
/// Interface for sensor age tracking service
/// </summary>
public interface ISensorAgeService : IDeviceAgeService
{
    /// <summary>
    /// Calculate sensor age information considering both sensor start and sensor change events
    /// </summary>
    /// <param name="treatments">List of treatments to analyze</param>
    /// <param name="currentTime">Current time in milliseconds since Unix epoch</param>
    /// <param name="preferences">Sensor-specific preferences</param>
    /// <returns>Sensor age information with both start and change tracking</returns>
    SensorAgeInfo CalculateSensorAge(
        List<Treatment> treatments,
        long currentTime,
        DeviceAgePreferences preferences
    );
}

/// <summary>
/// Interface for battery age tracking service
/// </summary>
public interface IBatteryAgeService : IDeviceAgeService { }
