using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Services;

/// <summary>
/// Cannula age service with 1:1 legacy JavaScript compatibility
/// Implements the exact algorithms from .LegacyApp/lib/plugins/cannulaage.js
/// </summary>
public class CannulaAgeService : BaseDeviceAgeService, ICannulaAgeService
{
    /// <summary>
    /// Get treatments valid for cannula age tracking (site change treatments)
    /// </summary>
    protected override IEnumerable<Treatment> GetValidTreatments(List<Treatment> treatments)
    {
        return treatments.Where(t =>
            string.Equals(t.EventType, "Site Change", StringComparison.OrdinalIgnoreCase)
            || string.Equals(t.EventType, "Cannula Change", StringComparison.OrdinalIgnoreCase)
            || string.Equals(t.EventType, "Insulin Change", StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// Get the cannula-specific notification group name
    /// </summary>
    protected override string GetNotificationGroup()
    {
        return "CAGE";
    }

    /// <summary>
    /// Get cannula-specific notification messages
    /// </summary>
    protected override (string urgent, string warn, string info) GetNotificationMessages()
    {
        return (
            urgent: "Cannula change overdue!",
            warn: "Time to change cannula",
            info: "Change cannula soon"
        );
    }

    /// <summary>
    /// Get the cannula device label for notifications
    /// </summary>
    protected override string GetDeviceLabel()
    {
        return "Cannula";
    }

    /// <summary>
    /// Get default preferences for cannula age tracking
    /// </summary>
    public static DeviceAgePreferences GetDefaultPreferences()
    {
        return new DeviceAgePreferences
        {
            Info = 44, // CAGE_INFO = 44
            Warn = 48, // CAGE_WARN = 48
            Urgent = 72, // CAGE_URGENT = 72
            Display = "hours",
            EnableAlerts = false,
        };
    }
}
