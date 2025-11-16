using Nocturne.API.Services;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services;

public class SensorAgeServiceTests
{
    private readonly SensorAgeService _service;

    public SensorAgeServiceTests()
    {
        _service = new SensorAgeService();
    }

    [Fact]
    public void CalculateSensorAge_WithNoTreatments_ReturnsNotFound()
    {
        // Arrange
        var treatments = new List<Treatment>();
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var preferences = SensorAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateSensorAge(treatments, currentTime, preferences);

        // Assert
        Assert.False(result.SensorStart.Found);
        Assert.False(result.SensorChange.Found);
        Assert.Equal("Sensor Start", result.Min);
    }

    [Fact]
    public void CalculateSensorAge_WithSensorStartData_ReturnsCorrectAge()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (150 * 60 * 60 * 1000); // 150 hours ago (6.25 days)
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Sensor Start",
                Mills = treatmentTime,
                Notes = "Started new sensor",
            },
        };
        var preferences = SensorAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateSensorAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.SensorStart.Found);
        Assert.False(result.SensorChange.Found);
        Assert.Equal("Sensor Start", result.Min);
        Assert.Equal(150, result.SensorStart.Age);
        Assert.Equal(6, result.SensorStart.Days);
        Assert.Equal(6, result.SensorStart.Hours);
        Assert.Equal("6d6h", result.SensorStart.Display);
    }

    [Fact]
    public void CalculateSensorAge_WithSensorChangeData_ReturnsCorrectAge()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (100 * 60 * 60 * 1000); // 100 hours ago
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Sensor Change",
                Mills = treatmentTime,
                Notes = "Changed sensor",
            },
        };
        var preferences = SensorAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateSensorAge(treatments, currentTime, preferences);

        // Assert
        Assert.False(result.SensorStart.Found);
        Assert.True(result.SensorChange.Found);
        Assert.Equal("Sensor Change", result.Min);
        Assert.Equal(100, result.SensorChange.Age);
        Assert.Equal(4, result.SensorChange.Days);
        Assert.Equal(4, result.SensorChange.Hours);
    }

    [Fact]
    public void CalculateSensorAge_WithBothStartAndChange_ReturnsNewerEvent()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var startTime = currentTime - (200 * 60 * 60 * 1000); // 200 hours ago
        var changeTime = currentTime - (50 * 60 * 60 * 1000); // 50 hours ago (newer)
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Sensor Start",
                Mills = startTime,
                Notes = "Started sensor",
            },
            new()
            {
                EventType = "Sensor Change",
                Mills = changeTime,
                Notes = "Changed sensor",
            },
        };
        var preferences = SensorAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateSensorAge(treatments, currentTime, preferences);

        // Assert
        Assert.False(result.SensorStart.Found); // Should be disabled when change is newer
        Assert.True(result.SensorChange.Found);
        Assert.Equal("Sensor Change", result.Min);
        Assert.Equal(50, result.SensorChange.Age);
    }

    [Fact]
    public void CalculateSensorAge_WithChangeOlderThanStart_ReturnsStart()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var changeTime = currentTime - (200 * 60 * 60 * 1000); // 200 hours ago (older)
        var startTime = currentTime - (50 * 60 * 60 * 1000); // 50 hours ago (newer)
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Sensor Start",
                Mills = startTime,
                Notes = "Started sensor",
            },
            new()
            {
                EventType = "Sensor Change",
                Mills = changeTime,
                Notes = "Changed sensor",
            },
        };
        var preferences = SensorAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateSensorAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.SensorStart.Found);
        Assert.True(result.SensorChange.Found);
        Assert.Equal("Sensor Start", result.Min);
        Assert.Equal(50, result.SensorStart.Age);
    }

    [Fact]
    public void CalculateSensorAge_WithWarnThreshold_SetsWarnLevel()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (164 * 60 * 60 * 1000); // 164 hours ago (default warn threshold)
        var treatments = new List<Treatment>
        {
            new() { EventType = "Sensor Start", Mills = treatmentTime },
        };
        var preferences = SensorAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateSensorAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.SensorStart.Found);
        Assert.Equal(164, result.SensorStart.Age);
        Assert.Equal(Levels.WARN, result.SensorStart.Level);
    }

    [Fact]
    public void CalculateSensorAge_WithUrgentThreshold_SetsUrgentLevel()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (166 * 60 * 60 * 1000); // 166 hours ago (default urgent threshold)
        var treatments = new List<Treatment>
        {
            new() { EventType = "Sensor Start", Mills = treatmentTime },
        };
        var preferences = SensorAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateSensorAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.SensorStart.Found);
        Assert.Equal(166, result.SensorStart.Age);
        Assert.Equal(Levels.URGENT, result.SensorStart.Level);
    }

    [Fact]
    public void CalculateSensorAge_WithAlertsEnabledAndExactThreshold_CreatesNotification()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (164 * 60 * 60 * 1000); // Exactly 164 hours ago
        var treatments = new List<Treatment>
        {
            new() { EventType = "Sensor Start", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 144,
            Warn = 164,
            Urgent = 166,
            Display = "days",
            EnableAlerts = true,
        };

        // Act
        var result = _service.CalculateSensorAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.SensorStart.Found);
        Assert.NotNull(result.SensorStart.Notification);
        Assert.Equal("Sensor age 6 days 20 hours", result.SensorStart.Notification.Title);
        Assert.Equal("Time to change/restart sensor", result.SensorStart.Notification.Message);
        Assert.Equal("SAGE", result.SensorStart.Notification.Group);
        Assert.Equal(Levels.WARN, result.SensorStart.Notification.Level);
    }

    [Fact]
    public void CalculateSensorAge_WithInvalidEventType_ReturnsNotFound()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (100 * 60 * 60 * 1000);
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Meal Bolus", // Invalid for sensor age
                Mills = treatmentTime,
            },
        };
        var preferences = SensorAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateSensorAge(treatments, currentTime, preferences);

        // Assert
        Assert.False(result.SensorStart.Found);
        Assert.False(result.SensorChange.Found);
        Assert.Equal("Sensor Start", result.Min);
    }

    [Fact]
    public void GetDefaultPreferences_ReturnsCorrectValues()
    {
        // Act
        var preferences = SensorAgeService.GetDefaultPreferences();

        // Assert
        Assert.Equal(144, preferences.Info); // 6 days
        Assert.Equal(164, preferences.Warn); // 7 days - 4 hours
        Assert.Equal(166, preferences.Urgent); // 7 days - 2 hours
        Assert.Equal("days", preferences.Display);
        Assert.False(preferences.EnableAlerts);
    }
}
