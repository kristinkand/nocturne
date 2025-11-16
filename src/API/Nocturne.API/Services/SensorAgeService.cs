using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Services;

/// <summary>
/// Sensor age service with 1:1 legacy JavaScript compatibility
/// Implements the exact algorithms from .LegacyApp/lib/plugins/sensorage.js
/// </summary>
public class SensorAgeService : BaseDeviceAgeService, ISensorAgeService
{
    /// <summary>
    /// Calculate sensor age information considering both sensor start and sensor change events
    /// </summary>
    public SensorAgeInfo CalculateSensorAge(
        List<Treatment> treatments,
        long currentTime,
        DeviceAgePreferences preferences
    )
    {
        var result = new SensorAgeInfo();

        // Calculate age for both event types
        var sensorStartTreatments = treatments
            .Where(t =>
                string.Equals(t.EventType, "Sensor Start", StringComparison.OrdinalIgnoreCase)
            )
            .ToList();
        var sensorChangeTreatments = treatments
            .Where(t =>
                string.Equals(t.EventType, "Sensor Change", StringComparison.OrdinalIgnoreCase)
            )
            .ToList();

        result.SensorStart = CalculateDeviceAge(sensorStartTreatments, currentTime, preferences);
        result.SensorChange = CalculateDeviceAge(sensorChangeTreatments, currentTime, preferences);

        // Determine which event is most recent and valid
        if (
            result.SensorChange.Found
            && result.SensorStart.Found
            && result.SensorChange.TreatmentDate >= result.SensorStart.TreatmentDate
        )
        {
            result.SensorStart.Found = false;
            result.Min = "Sensor Change";
        }
        else if (result.SensorStart.Found)
        {
            result.Min = "Sensor Start";
        }
        else if (result.SensorChange.Found)
        {
            result.Min = "Sensor Change";
        }

        // Set enhanced display for sensor age
        SetEnhancedDisplay(result.SensorStart);
        SetEnhancedDisplay(result.SensorChange);

        return result;
    }

    /// <summary>
    /// Get treatments valid for sensor age tracking (sensor start and sensor change treatments)
    /// </summary>
    protected override IEnumerable<Treatment> GetValidTreatments(List<Treatment> treatments)
    {
        return treatments.Where(t =>
            string.Equals(t.EventType, "Sensor Start", StringComparison.OrdinalIgnoreCase)
            || string.Equals(t.EventType, "Sensor Change", StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// Get the sensor-specific notification group name
    /// </summary>
    protected override string GetNotificationGroup()
    {
        return "SAGE";
    }

    /// <summary>
    /// Get sensor-specific notification messages
    /// </summary>
    protected override (string urgent, string warn, string info) GetNotificationMessages()
    {
        return (
            urgent: "Sensor change/restart overdue!",
            warn: "Time to change/restart sensor",
            info: "Change/restart sensor soon"
        );
    }

    /// <summary>
    /// Get the sensor device label for notifications
    /// </summary>
    protected override string GetDeviceLabel()
    {
        return "Sensor";
    }

    /// <summary>
    /// Set enhanced display with long format for sensor age
    /// </summary>
    private static void SetEnhancedDisplay(DeviceAgeInfo deviceInfo)
    {
        if (!deviceInfo.Found)
            return;

        deviceInfo.Display = string.Empty;
        var displayLong = string.Empty;

        if (deviceInfo.Age >= 24)
        {
            deviceInfo.Display += $"{deviceInfo.Days}d";
            displayLong += $"{deviceInfo.Days} days";
        }

        deviceInfo.Display += $"{deviceInfo.Hours}h";

        if (displayLong.Length > 0)
        {
            displayLong += " ";
        }
        displayLong += $"{deviceInfo.Hours} hours";

        // Store long display in notes field for compatibility
        if (string.IsNullOrEmpty(deviceInfo.Notes))
        {
            deviceInfo.Notes = displayLong;
        }
    }

    /// <summary>
    /// Override notification title for sensor age with days and hours
    /// </summary>
    public override DeviceAgeInfo CalculateDeviceAge(
        List<Treatment> treatments,
        long currentTime,
        DeviceAgePreferences preferences
    )
    {
        var result = base.CalculateDeviceAge(treatments, currentTime, preferences);

        // Override notification title for sensor age
        if (result.Notification != null)
        {
            result.Notification.Title = $"Sensor age {result.Days} days {result.Hours} hours";
        }

        return result;
    }

    /// <summary>
    /// Get default preferences for sensor age tracking
    /// </summary>
    public static DeviceAgePreferences GetDefaultPreferences()
    {
        return new DeviceAgePreferences
        {
            Info = 144, // 6 days in hours
            Warn = 164, // 7 days - 4 hours
            Urgent = 166, // 7 days - 2 hours
            Display = "days",
            EnableAlerts = false,
        };
    }
}
