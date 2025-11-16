using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Services;

/// <summary>
/// Battery age service with 1:1 legacy JavaScript compatibility
/// Implements the exact algorithms from .LegacyApp/lib/plugins/batteryage.js
/// </summary>
public class BatteryAgeService : BaseDeviceAgeService, IBatteryAgeService
{
    /// <summary>
    /// Get treatments valid for battery age tracking (pump battery change treatments)
    /// </summary>
    protected override IEnumerable<Treatment> GetValidTreatments(List<Treatment> treatments)
    {
        return treatments.Where(t =>
            string.Equals(t.EventType, "Pump Battery Change", StringComparison.OrdinalIgnoreCase)
            || string.Equals(t.EventType, "Battery Change", StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// Get the battery-specific notification group name
    /// </summary>
    protected override string GetNotificationGroup()
    {
        return "BAGE";
    }

    /// <summary>
    /// Get battery-specific notification messages
    /// </summary>
    protected override (string urgent, string warn, string info) GetNotificationMessages()
    {
        return (
            urgent: "Pump Battery change overdue!",
            warn: "Time to change pump battery",
            info: "Change pump battery soon"
        );
    }

    /// <summary>
    /// Get the battery device label for notifications
    /// </summary>
    protected override string GetDeviceLabel()
    {
        return "Pump battery";
    }

    /// <summary>
    /// Get default preferences for battery age tracking
    /// </summary>
    public static DeviceAgePreferences GetDefaultPreferences()
    {
        return new DeviceAgePreferences
        {
            Info = 312, // ~13 days
            Warn = 336, // ~14 days
            Urgent = 360, // ~15 days
            Display = "days",
            EnableAlerts = false,
        };
    }
}
