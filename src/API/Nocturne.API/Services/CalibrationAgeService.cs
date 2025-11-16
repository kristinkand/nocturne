using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Services;

/// <summary>
/// Calibration age service with 1:1 legacy JavaScript compatibility
/// Tracks time since last BG Check or Calibration treatment
/// </summary>
public class CalibrationAgeService : BaseDeviceAgeService, ICalibrationAgeService
{
    /// <summary>
    /// Get treatments valid for calibration age tracking (BG Check and Calibration treatments)
    /// </summary>
    protected override IEnumerable<Treatment> GetValidTreatments(List<Treatment> treatments)
    {
        return treatments.Where(t =>
            string.Equals(t.EventType, "BG Check", StringComparison.OrdinalIgnoreCase)
            || string.Equals(t.EventType, "Calibration", StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// Get the calibration-specific notification group name
    /// </summary>
    protected override string GetNotificationGroup()
    {
        return "CALIB";
    }

    /// <summary>
    /// Get calibration-specific notification messages
    /// </summary>
    protected override (string urgent, string warn, string info) GetNotificationMessages()
    {
        return (
            urgent: "Calibration overdue!",
            warn: "Time to calibrate sensor",
            info: "Consider calibrating sensor"
        );
    }

    /// <summary>
    /// Get the calibration device label for notifications
    /// </summary>
    protected override string GetDeviceLabel()
    {
        return "Calibration";
    }

    /// <summary>
    /// Get default preferences for calibration age tracking
    /// </summary>
    public static DeviceAgePreferences GetDefaultPreferences()
    {
        return new DeviceAgePreferences
        {
            Info = 24, // 1 day in hours
            Warn = 48, // 2 days in hours
            Urgent = 72, // 3 days in hours
            Display = "hours",
            EnableAlerts = false,
        };
    }
}
