using Nocturne.API.Services;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services;

public class BatteryAgeServiceTests
{
    private readonly BatteryAgeService _service;

    public BatteryAgeServiceTests()
    {
        _service = new BatteryAgeService();
    }

    [Fact]
    public void CalculateDeviceAge_WithNoTreatments_ReturnsNotFound()
    {
        // Arrange
        var treatments = new List<Treatment>();
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var preferences = BatteryAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.False(result.Found);
        Assert.Equal(0, result.Age);
        Assert.Equal("n/a", result.Display);
        Assert.Equal(Levels.NONE, result.Level);
    }

    [Fact]
    public void CalculateDeviceAge_WithBatteryChangeData_ReturnsCorrectAge()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (300 * 60 * 60 * 1000); // 300 hours ago (12.5 days)
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Pump Battery Change",
                Mills = treatmentTime,
                Notes = "Changed pump battery",
            },
        };
        var preferences = BatteryAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(300, result.Age);
        Assert.Equal(12, result.Days);
        Assert.Equal(12, result.Hours);
        Assert.Equal("12d12h", result.Display); // Default display is "days"
        Assert.Equal("Changed pump battery", result.Notes);
        Assert.Equal(treatmentTime, result.TreatmentDate);
        Assert.Equal(Levels.NONE, result.Level);
    }

    [Fact]
    public void CalculateDeviceAge_WithBatteryChangeEventType_ReturnsCorrectAge()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (250 * 60 * 60 * 1000); // 250 hours ago
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Battery Change", // Alternative event type
                Mills = treatmentTime,
                Notes = "Battery replaced",
            },
        };
        var preferences = BatteryAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(250, result.Age);
        Assert.Equal("Battery replaced", result.Notes);
    }

    [Fact]
    public void CalculateDeviceAge_WithMultipleTreatments_ReturnsLatest()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var olderTreatmentTime = currentTime - (400 * 60 * 60 * 1000); // 400 hours ago
        var newerTreatmentTime = currentTime - (100 * 60 * 60 * 1000); // 100 hours ago
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Pump Battery Change",
                Mills = olderTreatmentTime,
                Notes = "Older change",
            },
            new()
            {
                EventType = "Pump Battery Change",
                Mills = newerTreatmentTime,
                Notes = "Newer change",
            },
        };
        var preferences = BatteryAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(100, result.Age);
        Assert.Equal("Newer change", result.Notes);
        Assert.Equal(newerTreatmentTime, result.TreatmentDate);
    }

    [Fact]
    public void CalculateDeviceAge_WithInfoThreshold_SetsInfoLevel()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (312 * 60 * 60 * 1000); // 312 hours ago (default info threshold)
        var treatments = new List<Treatment>
        {
            new() { EventType = "Pump Battery Change", Mills = treatmentTime },
        };
        var preferences = BatteryAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(312, result.Age);
        Assert.Equal(Levels.INFO, result.Level);
    }

    [Fact]
    public void CalculateDeviceAge_WithWarnThreshold_SetsWarnLevel()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (336 * 60 * 60 * 1000); // 336 hours ago (default warn threshold)
        var treatments = new List<Treatment>
        {
            new() { EventType = "Pump Battery Change", Mills = treatmentTime },
        };
        var preferences = BatteryAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(336, result.Age);
        Assert.Equal(Levels.WARN, result.Level);
    }

    [Fact]
    public void CalculateDeviceAge_WithUrgentThreshold_SetsUrgentLevel()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (360 * 60 * 60 * 1000); // 360 hours ago (default urgent threshold)
        var treatments = new List<Treatment>
        {
            new() { EventType = "Pump Battery Change", Mills = treatmentTime },
        };
        var preferences = BatteryAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(360, result.Age);
        Assert.Equal(Levels.URGENT, result.Level);
    }

    [Fact]
    public void CalculateDeviceAge_WithHoursDisplay_ShowsHoursOnly()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (300 * 60 * 60 * 1000); // 300 hours ago
        var treatments = new List<Treatment>
        {
            new() { EventType = "Pump Battery Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 312,
            Warn = 336,
            Urgent = 360,
            Display = "hours", // Different from default
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(300, result.Age);
        Assert.Equal("300h", result.Display);
    }

    [Fact]
    public void CalculateDeviceAge_WithAlertsEnabledAndExactThreshold_CreatesNotification()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (336 * 60 * 60 * 1000); // Exactly 336 hours ago
        var treatments = new List<Treatment>
        {
            new() { EventType = "Pump Battery Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 312,
            Warn = 336,
            Urgent = 360,
            Display = "days",
            EnableAlerts = true,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.NotNull(result.Notification);
        Assert.Equal("Pump battery age 336 hours", result.Notification.Title);
        Assert.Equal("Time to change pump battery", result.Notification.Message);
        Assert.Equal("BAGE", result.Notification.Group);
        Assert.Equal(Levels.WARN, result.Notification.Level);
    }

    [Fact]
    public void CalculateDeviceAge_WithInvalidEventType_ReturnsNotFound()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (300 * 60 * 60 * 1000);
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Site Change", // Invalid for battery age
                Mills = treatmentTime,
            },
        };
        var preferences = BatteryAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.False(result.Found);
        Assert.Equal("n/a", result.Display);
    }

    [Fact]
    public void GetDefaultPreferences_ReturnsCorrectValues()
    {
        // Act
        var preferences = BatteryAgeService.GetDefaultPreferences();

        // Assert
        Assert.Equal(312, preferences.Info); // ~13 days
        Assert.Equal(336, preferences.Warn); // ~14 days
        Assert.Equal(360, preferences.Urgent); // ~15 days
        Assert.Equal("days", preferences.Display);
        Assert.False(preferences.EnableAlerts);
    }
}
