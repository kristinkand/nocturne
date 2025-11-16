using Nocturne.API.Services;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services;

public class CannulaAgeServiceTests
{
    private readonly CannulaAgeService _service;

    public CannulaAgeServiceTests()
    {
        _service = new CannulaAgeService();
    }

    [Fact]
    public void CalculateDeviceAge_WithNoTreatments_ReturnsNotFound()
    {
        // Arrange
        var treatments = new List<Treatment>();
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var preferences = CannulaAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.False(result.Found);
        Assert.Equal(0, result.Age);
        Assert.Equal("n/a", result.Display);
        Assert.Equal(Levels.NONE, result.Level);
    }

    [Fact]
    public void CalculateDeviceAge_WithSiteChangeData_ReturnsCorrectAge()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (25 * 60 * 60 * 1000); // 25 hours ago
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Site Change",
                Mills = treatmentTime,
                Notes = "Changed cannula",
            },
        };
        var preferences = CannulaAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(25, result.Age);
        Assert.Equal(1, result.Days);
        Assert.Equal(1, result.Hours);
        Assert.Equal("25h", result.Display);
        Assert.Equal("Changed cannula", result.Notes);
        Assert.Equal(treatmentTime, result.TreatmentDate);
        Assert.Equal(Levels.NONE, result.Level);
    }

    [Fact]
    public void CalculateDeviceAge_WithMultipleTreatments_ReturnsLatest()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var olderTreatmentTime = currentTime - (50 * 60 * 60 * 1000); // 50 hours ago
        var newerTreatmentTime = currentTime - (10 * 60 * 60 * 1000); // 10 hours ago
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Site Change",
                Mills = olderTreatmentTime,
                Notes = "Older change",
            },
            new()
            {
                EventType = "Site Change",
                Mills = newerTreatmentTime,
                Notes = "Newer change",
            },
        };
        var preferences = CannulaAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(10, result.Age);
        Assert.Equal("Newer change", result.Notes);
        Assert.Equal(newerTreatmentTime, result.TreatmentDate);
    }

    [Fact]
    public void CalculateDeviceAge_WithWarnThreshold_SetsWarnLevel()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (48 * 60 * 60 * 1000); // 48 hours ago (default warn threshold)
        var treatments = new List<Treatment>
        {
            new() { EventType = "Site Change", Mills = treatmentTime },
        };
        var preferences = CannulaAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(48, result.Age);
        Assert.Equal(Levels.WARN, result.Level);
    }

    [Fact]
    public void CalculateDeviceAge_WithUrgentThreshold_SetsUrgentLevel()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (72 * 60 * 60 * 1000); // 72 hours ago (default urgent threshold)
        var treatments = new List<Treatment>
        {
            new() { EventType = "Site Change", Mills = treatmentTime },
        };
        var preferences = CannulaAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(72, result.Age);
        Assert.Equal(Levels.URGENT, result.Level);
    }

    [Fact]
    public void CalculateDeviceAge_WithDaysDisplay_ShowsDaysAndHours()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (50 * 60 * 60 * 1000); // 50 hours ago
        var treatments = new List<Treatment>
        {
            new() { EventType = "Site Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 44,
            Warn = 48,
            Urgent = 72,
            Display = "days",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(50, result.Age);
        Assert.Equal(2, result.Days);
        Assert.Equal(2, result.Hours);
        Assert.Equal("2d2h", result.Display);
    }

    [Fact]
    public void CalculateDeviceAge_WithAlertsEnabledAndExactThreshold_CreatesNotification()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (48 * 60 * 60 * 1000); // Exactly 48 hours ago
        var treatments = new List<Treatment>
        {
            new() { EventType = "Site Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 44,
            Warn = 48,
            Urgent = 72,
            Display = "hours",
            EnableAlerts = true,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.NotNull(result.Notification);
        Assert.Equal("Cannula age 48 hours", result.Notification.Title);
        Assert.Equal("Time to change cannula", result.Notification.Message);
        Assert.Equal("CAGE", result.Notification.Group);
        Assert.Equal(Levels.WARN, result.Notification.Level);
    }

    [Fact]
    public void CalculateDeviceAge_WithInvalidEventType_ReturnsNotFound()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (25 * 60 * 60 * 1000);
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Meal Bolus", // Invalid for cannula age
                Mills = treatmentTime,
            },
        };
        var preferences = CannulaAgeService.GetDefaultPreferences();

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
        var preferences = CannulaAgeService.GetDefaultPreferences();

        // Assert
        Assert.Equal(44, preferences.Info);
        Assert.Equal(48, preferences.Warn);
        Assert.Equal(72, preferences.Urgent);
        Assert.Equal("hours", preferences.Display);
        Assert.False(preferences.EnableAlerts);
    }
}
