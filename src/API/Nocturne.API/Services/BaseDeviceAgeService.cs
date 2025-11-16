using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Services;

/// <summary>
/// Base service for device age tracking with 1:1 legacy JavaScript compatibility
/// Implements common logic shared by cannula, sensor, and battery age services
/// </summary>
public abstract class BaseDeviceAgeService : IDeviceAgeService
{
    /// <summary>
    /// Calculate age information for a specific device type based on treatments
    /// </summary>
    public virtual DeviceAgeInfo CalculateDeviceAge(
        List<Treatment> treatments,
        long currentTime,
        DeviceAgePreferences preferences
    )
    {
        var deviceInfo = new DeviceAgeInfo
        {
            Found = false,
            Age = 0,
            TreatmentDate = null,
            Level = Levels.NONE,
        };

        var prevDate = 0L;
        var validTreatments = GetValidTreatments(treatments);

        foreach (var treatment in validTreatments)
        {
            var treatmentDate = treatment.Mills;
            if (treatmentDate > prevDate && treatmentDate <= currentTime)
            {
                prevDate = treatmentDate;
                deviceInfo.TreatmentDate = treatmentDate;

                var ageHours = CalculateAgeInHours(currentTime, treatmentDate);
                var days = ageHours / 24;
                var hours = ageHours - (days * 24);

                if (!deviceInfo.Found || (ageHours >= 0 && ageHours < deviceInfo.Age))
                {
                    deviceInfo.Found = true;
                    deviceInfo.Age = ageHours;
                    deviceInfo.Days = days;
                    deviceInfo.Hours = hours;
                    deviceInfo.Notes = treatment.Notes;
                    deviceInfo.MinFractions = CalculateMinuteFractions(
                        currentTime,
                        treatmentDate,
                        ageHours
                    );
                }
            }
        }

        if (deviceInfo.Found)
        {
            SetNotificationLevel(deviceInfo, preferences);
            SetDisplayString(deviceInfo, preferences);
            SetNotification(deviceInfo, preferences);
        }

        return deviceInfo;
    }

    /// <summary>
    /// Get treatments valid for this device type
    /// </summary>
    protected abstract IEnumerable<Treatment> GetValidTreatments(List<Treatment> treatments);

    /// <summary>
    /// Get the device-specific notification group name
    /// </summary>
    protected abstract string GetNotificationGroup();

    /// <summary>
    /// Get device-specific notification messages
    /// </summary>
    protected abstract (string urgent, string warn, string info) GetNotificationMessages();

    /// <summary>
    /// Get the device label for notifications
    /// </summary>
    protected abstract string GetDeviceLabel();

    /// <summary>
    /// Calculate age in hours between two timestamps
    /// </summary>
    private static int CalculateAgeInHours(long currentTime, long treatmentTime)
    {
        var currentDateTime = DateTimeOffset.FromUnixTimeMilliseconds(currentTime);
        var treatmentDateTime = DateTimeOffset.FromUnixTimeMilliseconds(treatmentTime);
        return (int)Math.Floor((currentDateTime - treatmentDateTime).TotalHours);
    }

    /// <summary>
    /// Calculate minute fractions for precise timing
    /// </summary>
    private static int CalculateMinuteFractions(long currentTime, long treatmentTime, int ageHours)
    {
        var currentDateTime = DateTimeOffset.FromUnixTimeMilliseconds(currentTime);
        var treatmentDateTime = DateTimeOffset.FromUnixTimeMilliseconds(treatmentTime);
        var totalMinutes = (int)Math.Floor((currentDateTime - treatmentDateTime).TotalMinutes);
        return totalMinutes - (ageHours * 60);
    }

    /// <summary>
    /// Set notification level based on age and preferences
    /// </summary>
    private void SetNotificationLevel(DeviceAgeInfo deviceInfo, DeviceAgePreferences preferences)
    {
        if (deviceInfo.Age >= preferences.Urgent)
        {
            deviceInfo.Level = Levels.URGENT;
        }
        else if (deviceInfo.Age >= preferences.Warn)
        {
            deviceInfo.Level = Levels.WARN;
        }
        else if (deviceInfo.Age >= preferences.Info)
        {
            deviceInfo.Level = Levels.INFO;
        }
        else
        {
            deviceInfo.Level = Levels.NONE;
        }
    }

    /// <summary>
    /// Set display string based on preferences
    /// </summary>
    private static void SetDisplayString(DeviceAgeInfo deviceInfo, DeviceAgePreferences preferences)
    {
        if (preferences.Display == "days" && deviceInfo.Found)
        {
            deviceInfo.Display = string.Empty;
            if (deviceInfo.Age >= 24)
            {
                deviceInfo.Display += $"{deviceInfo.Days}d";
            }
            deviceInfo.Display += $"{deviceInfo.Hours}h";
        }
        else
        {
            deviceInfo.Display = deviceInfo.Found ? $"{deviceInfo.Age}h" : "n/a";
        }
    }

    /// <summary>
    /// Set notification if alerts are enabled and conditions are met
    /// </summary>
    private void SetNotification(DeviceAgeInfo deviceInfo, DeviceAgePreferences preferences)
    {
        if (!preferences.EnableAlerts || deviceInfo.Level == Levels.NONE)
            return;

        var shouldNotify = ShouldSendNotification(deviceInfo, preferences);
        if (!shouldNotify)
            return;

        // Allow for 20 minute period after a full hour during which we'll alert the user
        if (deviceInfo.MinFractions > 20)
            return;

        var messages = GetNotificationMessages();
        var (message, sound) = deviceInfo.Level switch
        {
            var level when level >= Levels.URGENT => (messages.urgent, "persistent"),
            var level when level >= Levels.WARN => (messages.warn, "incoming"),
            var level when level >= Levels.INFO => (messages.info, "incoming"),
            _ => (string.Empty, "incoming"),
        };

        if (string.IsNullOrEmpty(message))
            return;

        deviceInfo.Notification = new DeviceAgeNotification
        {
            Title = $"{GetDeviceLabel()} age {deviceInfo.Age} hours",
            Message = message,
            PushoverSound = sound,
            Level = deviceInfo.Level,
            Group = GetNotificationGroup(),
        };
    }

    /// <summary>
    /// Determine if a notification should be sent based on exact age matching
    /// </summary>
    private static bool ShouldSendNotification(
        DeviceAgeInfo deviceInfo,
        DeviceAgePreferences preferences
    )
    {
        return deviceInfo.Age == preferences.Urgent
            || deviceInfo.Age == preferences.Warn
            || deviceInfo.Age == preferences.Info;
    }
}

/// <summary>
/// Static class containing notification level constants for 1:1 legacy compatibility
/// </summary>
public static class Levels
{
    public const int URGENT = 2;
    public const int WARN = 1;
    public const int INFO = 0;
    public const int LOW = -1;
    public const int LOWEST = -2;
    public const int NONE = -3;

    /// <summary>
    /// Convert level constant to display text
    /// </summary>
    public static string ToDisplay(int level)
    {
        return level switch
        {
            URGENT => "Urgent",
            WARN => "Warning",
            INFO => "Info",
            LOW => "Low",
            LOWEST => "Lowest",
            NONE => "None",
            _ => "Unknown",
        };
    }

    /// <summary>
    /// Convert level constant to lowercase text
    /// </summary>
    public static string ToLowerCase(int level)
    {
        return level switch
        {
            URGENT => "urgent",
            WARN => "warn",
            INFO => "info",
            LOW => "low",
            LOWEST => "lowest",
            NONE => "none",
            _ => "unknown",
        };
    }
}
