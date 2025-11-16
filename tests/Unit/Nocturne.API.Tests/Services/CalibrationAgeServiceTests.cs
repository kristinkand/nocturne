using Nocturne.API.Services;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services;

public class CalibrationAgeServiceTests
{
    private readonly CalibrationAgeService _service;

    public CalibrationAgeServiceTests()
    {
        _service = new CalibrationAgeService();
    }

    [Fact]
    public void CalculateDeviceAge_WithNoTreatments_ReturnsNotFound()
    {
        // Arrange
        var treatments = new List<Treatment>();
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var preferences = CalibrationAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.False(result.Found);
        Assert.Equal(0, result.Age);
        Assert.Equal("n/a", result.Display);
        Assert.Equal(Levels.NONE, result.Level);
    }

    [Fact]
    public void CalculateDeviceAge_WithBgCheckData_ReturnsCorrectAge()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (5 * 60 * 60 * 1000); // 5 hours ago
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "BG Check",
                Mills = treatmentTime,
                Notes = "Finger stick calibration",
                Glucose = 120,
                GlucoseType = "Finger",
            },
        };
        var preferences = CalibrationAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(5, result.Age);
        Assert.Equal(0, result.Days);
        Assert.Equal(5, result.Hours);
        Assert.Equal("Finger stick calibration", result.Notes);
        Assert.Equal(treatmentTime, result.TreatmentDate);
        Assert.Equal(Levels.NONE, result.Level); // 5 hours is below info threshold of 24
    }

    [Fact]
    public void CalculateDeviceAge_WithCalibrationData_ReturnsCorrectAge()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (30 * 60 * 60 * 1000); // 30 hours ago
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Calibration",
                Mills = treatmentTime,
                Notes = "CGM calibration",
                Glucose = 115,
                GlucoseType = "Finger",
            },
        };
        var preferences = CalibrationAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(30, result.Age);
        Assert.Equal(1, result.Days);
        Assert.Equal(6, result.Hours);
        Assert.Equal("CGM calibration", result.Notes);
        Assert.Equal(treatmentTime, result.TreatmentDate);
        Assert.Equal(Levels.INFO, result.Level); // 30 hours is above info threshold of 24
    }

    [Fact]
    public void CalculateDeviceAge_WithMultipleTreatments_ReturnsLatestAge()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var olderTreatmentTime = currentTime - (50 * 60 * 60 * 1000); // 50 hours ago
        var newerTreatmentTime = currentTime - (10 * 60 * 60 * 1000); // 10 hours ago

        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "BG Check",
                Mills = olderTreatmentTime,
                Notes = "Older calibration",
                Glucose = 120,
            },
            new()
            {
                EventType = "Calibration",
                Mills = newerTreatmentTime,
                Notes = "Newer calibration",
                Glucose = 110,
            },
        };
        var preferences = CalibrationAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(10, result.Age);
        Assert.Equal(0, result.Days);
        Assert.Equal(10, result.Hours);
        Assert.Equal("Newer calibration", result.Notes);
        Assert.Equal(newerTreatmentTime, result.TreatmentDate);
        Assert.Equal(Levels.NONE, result.Level);
    }

    [Fact]
    public void CalculateDeviceAge_WithWarnThreshold_ReturnsWarnLevel()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (50 * 60 * 60 * 1000); // 50 hours ago
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "BG Check",
                Mills = treatmentTime,
                Notes = "Old calibration",
                Glucose = 125,
            },
        };
        var preferences = CalibrationAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(50, result.Age);
        Assert.Equal(2, result.Days);
        Assert.Equal(2, result.Hours);
        Assert.Equal(Levels.WARN, result.Level); // 50 hours is above warn threshold of 48
    }

    [Fact]
    public void CalculateDeviceAge_WithUrgentThreshold_ReturnsUrgentLevel()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (80 * 60 * 60 * 1000); // 80 hours ago
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Calibration",
                Mills = treatmentTime,
                Notes = "Very old calibration",
                Glucose = 130,
            },
        };
        var preferences = CalibrationAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(80, result.Age);
        Assert.Equal(3, result.Days);
        Assert.Equal(8, result.Hours);
        Assert.Equal(Levels.URGENT, result.Level); // 80 hours is above urgent threshold of 72
    }

    [Fact]
    public void CalculateDeviceAge_WithInvalidEventType_ReturnsNotFound()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (5 * 60 * 60 * 1000);
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Meal Bolus", // Invalid for calibration
                Mills = treatmentTime,
                Notes = "Not a calibration",
                Insulin = 2.5,
            },
        };
        var preferences = CalibrationAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.False(result.Found);
        Assert.Equal(0, result.Age);
        Assert.Equal(Levels.NONE, result.Level);
    }

    [Fact]
    public void CalculateDeviceAge_WithFutureTreatment_IgnoresFutureTreatment()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var pastTreatmentTime = currentTime - (5 * 60 * 60 * 1000); // 5 hours ago
        var futureTreatmentTime = currentTime + (5 * 60 * 60 * 1000); // 5 hours in future

        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "BG Check",
                Mills = pastTreatmentTime,
                Notes = "Valid calibration",
                Glucose = 120,
            },
            new()
            {
                EventType = "BG Check",
                Mills = futureTreatmentTime,
                Notes = "Future calibration", // Should be ignored
                Glucose = 110,
            },
        };
        var preferences = CalibrationAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(5, result.Age);
        Assert.Equal("Valid calibration", result.Notes);
        Assert.Equal(pastTreatmentTime, result.TreatmentDate);
    }

    [Fact]
    public void GetDefaultPreferences_ReturnsCorrectDefaults()
    {
        // Act
        var preferences = CalibrationAgeService.GetDefaultPreferences();

        // Assert
        Assert.Equal(24, preferences.Info);
        Assert.Equal(48, preferences.Warn);
        Assert.Equal(72, preferences.Urgent);
        Assert.Equal("hours", preferences.Display);
        Assert.False(preferences.EnableAlerts);
    }

    [Theory]
    [InlineData("BG Check")]
    [InlineData("bg check")]
    [InlineData("Calibration")]
    [InlineData("calibration")]
    [InlineData("CALIBRATION")]
    public void CalculateDeviceAge_WithValidEventTypes_CaseInsensitive_ReturnsFound(
        string eventType
    )
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (1 * 60 * 60 * 1000); // 1 hour ago
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = eventType,
                Mills = treatmentTime,
                Notes = "Test calibration",
                Glucose = 120,
            },
        };
        var preferences = CalibrationAgeService.GetDefaultPreferences();

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(1, result.Age);
    }
}
