using Nocturne.API.Services;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services;

/// <summary>
/// Comprehensive unit tests for BaseDeviceAgeService with 1:1 legacy parity
/// Tests the abstract base class through a concrete test implementation
/// </summary>
public class BaseDeviceAgeServiceTests
{
    private readonly TestDeviceAgeService _service;

    public BaseDeviceAgeServiceTests()
    {
        _service = new TestDeviceAgeService();
    }

    #region Basic Functionality Tests

    [Fact]
    public void CalculateDeviceAge_WithNoTreatments_ReturnsNotFound()
    {
        // Arrange
        var treatments = new List<Treatment>();
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var preferences = new DeviceAgePreferences
        {
            Info = 20,
            Warn = 40,
            Urgent = 60,
            Display = "hours",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.False(result.Found);
        Assert.Equal(0, result.Age);
        Assert.Equal(0, result.Days);
        Assert.Equal(0, result.Hours);
        Assert.Null(result.TreatmentDate);
        Assert.Null(result.Notes);
        Assert.Equal(0, result.MinFractions);
        Assert.Equal(Levels.NONE, result.Level);
        Assert.Equal("n/a", result.Display);
        Assert.Null(result.Notification);
    }

    [Fact]
    public void CalculateDeviceAge_WithSingleValidTreatment_ReturnsCorrectAge()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (25 * 60 * 60 * 1000); // 25 hours ago
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Test Change",
                Mills = treatmentTime,
                Notes = "Test device change",
            },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 20,
            Warn = 40,
            Urgent = 60,
            Display = "hours",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(25, result.Age);
        Assert.Equal(1, result.Days);
        Assert.Equal(1, result.Hours);
        Assert.Equal(treatmentTime, result.TreatmentDate);
        Assert.Equal("Test device change", result.Notes);
        Assert.Equal(Levels.INFO, result.Level); // 25 hours >= 20 (info) but < 40 (warn), so should be INFO level
        Assert.Equal("25h", result.Display);
    }

    [Fact]
    public void CalculateDeviceAge_WithExactHourAge_CalculatesCorrectly()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (48 * 60 * 60 * 1000); // Exactly 48 hours ago
        var treatments = new List<Treatment>
        {
            new() { EventType = "Test Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 20,
            Warn = 40,
            Urgent = 60,
            Display = "hours",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(48, result.Age);
        Assert.Equal(2, result.Days);
        Assert.Equal(0, result.Hours);
        Assert.InRange(result.MinFractions, -1, 1); // Should be close to 0 for exact hours
    }

    #endregion

    #region Multiple Treatment Tests

    [Fact]
    public void CalculateDeviceAge_WithMultipleTreatments_SelectsMostRecent()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var olderTreatmentTime = currentTime - (50 * 60 * 60 * 1000); // 50 hours ago
        var newerTreatmentTime = currentTime - (10 * 60 * 60 * 1000); // 10 hours ago
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Test Change",
                Mills = olderTreatmentTime,
                Notes = "Older change",
            },
            new()
            {
                EventType = "Test Change",
                Mills = newerTreatmentTime,
                Notes = "Newer change",
            },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 20,
            Warn = 40,
            Urgent = 60,
            Display = "hours",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(10, result.Age);
        Assert.Equal(newerTreatmentTime, result.TreatmentDate);
        Assert.Equal("Newer change", result.Notes);
    }

    [Fact]
    public void CalculateDeviceAge_WithInvalidTreatments_IgnoresInvalid()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var validTreatmentTime = currentTime - (10 * 60 * 60 * 1000); // 10 hours ago
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Invalid Change", // Will be filtered out by test implementation
                Mills = currentTime - (5 * 60 * 60 * 1000),
            },
            new()
            {
                EventType = "Test Change",
                Mills = validTreatmentTime,
                Notes = "Valid change",
            },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 20,
            Warn = 40,
            Urgent = 60,
            Display = "hours",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(10, result.Age);
        Assert.Equal(validTreatmentTime, result.TreatmentDate);
        Assert.Equal("Valid change", result.Notes);
    }

    #endregion

    #region Threshold and Notification Level Tests

    [Fact]
    public void CalculateDeviceAge_WithInfoThreshold_SetsInfoLevel()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (25 * 60 * 60 * 1000); // 25 hours ago
        var treatments = new List<Treatment>
        {
            new() { EventType = "Test Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 20,
            Warn = 40,
            Urgent = 60,
            Display = "hours",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.Equal(Levels.INFO, result.Level);
    }

    [Fact]
    public void CalculateDeviceAge_WithWarnThreshold_SetsWarnLevel()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (45 * 60 * 60 * 1000); // 45 hours ago
        var treatments = new List<Treatment>
        {
            new() { EventType = "Test Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 20,
            Warn = 40,
            Urgent = 60,
            Display = "hours",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.Equal(Levels.WARN, result.Level);
    }

    [Fact]
    public void CalculateDeviceAge_WithUrgentThreshold_SetsUrgentLevel()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (65 * 60 * 60 * 1000); // 65 hours ago
        var treatments = new List<Treatment>
        {
            new() { EventType = "Test Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 20,
            Warn = 40,
            Urgent = 60,
            Display = "hours",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.Equal(Levels.URGENT, result.Level);
    }

    [Fact]
    public void CalculateDeviceAge_BelowAllThresholds_SetsNoneLevel()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (15 * 60 * 60 * 1000); // 15 hours ago
        var treatments = new List<Treatment>
        {
            new() { EventType = "Test Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 20,
            Warn = 40,
            Urgent = 60,
            Display = "hours",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.Equal(Levels.NONE, result.Level);
    }

    #endregion

    #region Display Format Tests

    [Fact]
    public void CalculateDeviceAge_WithHoursDisplay_ShowsHoursFormat()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (37 * 60 * 60 * 1000); // 37 hours ago
        var treatments = new List<Treatment>
        {
            new() { EventType = "Test Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 20,
            Warn = 40,
            Urgent = 60,
            Display = "hours",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.Equal("37h", result.Display);
    }

    [Fact]
    public void CalculateDeviceAge_WithDaysDisplayUnder24Hours_ShowsOnlyHours()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (15 * 60 * 60 * 1000); // 15 hours ago
        var treatments = new List<Treatment>
        {
            new() { EventType = "Test Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 10,
            Warn = 30,
            Urgent = 50,
            Display = "days",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.Equal("15h", result.Display);
    }

    [Fact]
    public void CalculateDeviceAge_WithDaysDisplayOver24Hours_ShowsDaysAndHours()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (37 * 60 * 60 * 1000); // 37 hours ago (1 day 13 hours)
        var treatments = new List<Treatment>
        {
            new() { EventType = "Test Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 20,
            Warn = 40,
            Urgent = 60,
            Display = "days",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.Equal("1d13h", result.Display);
    }

    [Fact]
    public void CalculateDeviceAge_WithDaysDisplayExactDays_ShowsOnlyDays()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (48 * 60 * 60 * 1000); // Exactly 48 hours ago (2 days 0 hours)
        var treatments = new List<Treatment>
        {
            new() { EventType = "Test Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 20,
            Warn = 40,
            Urgent = 60,
            Display = "days",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.Equal("2d0h", result.Display);
    }

    #endregion

    #region Notification Tests

    [Fact]
    public void CalculateDeviceAge_WithAlertsDisabled_DoesNotCreateNotification()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (45 * 60 * 60 * 1000); // 45 hours ago (warn level)
        var treatments = new List<Treatment>
        {
            new() { EventType = "Test Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 20,
            Warn = 40,
            Urgent = 60,
            Display = "hours",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.Null(result.Notification);
    }

    [Fact]
    public void CalculateDeviceAge_WithExactWarnThresholdAndAlertsEnabled_CreatesWarnNotification()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (40 * 60 * 60 * 1000); // Exactly 40 hours ago (warn threshold)
        var treatments = new List<Treatment>
        {
            new() { EventType = "Test Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 20,
            Warn = 40,
            Urgent = 60,
            Display = "hours",
            EnableAlerts = true,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.NotNull(result.Notification);
        Assert.Equal("Test device age 40 hours", result.Notification.Title);
        Assert.Equal("Test device warn message", result.Notification.Message);
        Assert.Equal("incoming", result.Notification.PushoverSound);
        Assert.Equal(Levels.WARN, result.Notification.Level);
        Assert.Equal("TEST", result.Notification.Group);
    }

    [Fact]
    public void CalculateDeviceAge_WithExactUrgentThresholdAndAlertsEnabled_CreatesUrgentNotification()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (60 * 60 * 60 * 1000); // Exactly 60 hours ago (urgent threshold)
        var treatments = new List<Treatment>
        {
            new() { EventType = "Test Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 20,
            Warn = 40,
            Urgent = 60,
            Display = "hours",
            EnableAlerts = true,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.NotNull(result.Notification);
        Assert.Equal("Test device age 60 hours", result.Notification.Title);
        Assert.Equal("Test device urgent message", result.Notification.Message);
        Assert.Equal("persistent", result.Notification.PushoverSound);
        Assert.Equal(Levels.URGENT, result.Notification.Level);
        Assert.Equal("TEST", result.Notification.Group);
    }

    [Fact]
    public void CalculateDeviceAge_WithExactInfoThresholdAndAlertsEnabled_CreatesInfoNotification()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (20 * 60 * 60 * 1000); // Exactly 20 hours ago (info threshold)
        var treatments = new List<Treatment>
        {
            new() { EventType = "Test Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 20,
            Warn = 40,
            Urgent = 60,
            Display = "hours",
            EnableAlerts = true,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.NotNull(result.Notification);
        Assert.Equal("Test device age 20 hours", result.Notification.Title);
        Assert.Equal("Test device info message", result.Notification.Message);
        Assert.Equal("incoming", result.Notification.PushoverSound);
        Assert.Equal(Levels.INFO, result.Notification.Level);
        Assert.Equal("TEST", result.Notification.Group);
    }

    [Fact]
    public void CalculateDeviceAge_WithNonExactThresholdAge_DoesNotCreateNotification()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (41 * 60 * 60 * 1000); // 41 hours ago (warn level but not exact threshold)
        var treatments = new List<Treatment>
        {
            new() { EventType = "Test Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 20,
            Warn = 40,
            Urgent = 60,
            Display = "hours",
            EnableAlerts = true,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.Null(result.Notification);
    }

    [Fact]
    public void CalculateDeviceAge_WithMinuteFractionsOver20_DoesNotCreateNotification()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // Create a time that results in 40 hours + 25 minutes (over the 20 minute window)
        var treatmentTime = currentTime - (40 * 60 * 60 * 1000) - (25 * 60 * 1000);
        var treatments = new List<Treatment>
        {
            new() { EventType = "Test Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 20,
            Warn = 40,
            Urgent = 60,
            Display = "hours",
            EnableAlerts = true,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.Equal(40, result.Age); // Should still be 40 hours
        Assert.True(result.MinFractions > 20); // Should be around 25 minutes
        Assert.Null(result.Notification); // Should not create notification due to minute window
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public void CalculateDeviceAge_WithFutureTreatment_IgnoresFutureTreatment()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var futureTime = currentTime + (10 * 60 * 60 * 1000); // 10 hours in future
        var pastTime = currentTime - (20 * 60 * 60 * 1000); // 20 hours ago
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Test Change",
                Mills = futureTime,
                Notes = "Future treatment",
            },
            new()
            {
                EventType = "Test Change",
                Mills = pastTime,
                Notes = "Past treatment",
            },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 10,
            Warn = 30,
            Urgent = 50,
            Display = "hours",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(20, result.Age);
        Assert.Equal(pastTime, result.TreatmentDate);
        Assert.Equal("Past treatment", result.Notes);
    }

    [Fact]
    public void CalculateDeviceAge_WithZeroCurrentTime_HandlesGracefully()
    {
        // Arrange
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Test Change",
                Mills = 1000 * 60 * 60 * 24, // 1 day in milliseconds from epoch
            },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 10,
            Warn = 30,
            Urgent = 50,
            Display = "hours",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, 0, preferences);

        // Assert
        Assert.False(result.Found); // Should not find treatment since current time is before treatment
    }

    [Fact]
    public void CalculateDeviceAge_WithVeryLargeAge_HandlesCorrectly()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var veryOldTime = currentTime - (365 * 24 * 60 * 60 * 1000L); // 1 year ago
        var treatments = new List<Treatment>
        {
            new() { EventType = "Test Change", Mills = veryOldTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 100,
            Warn = 200,
            Urgent = 300,
            Display = "days",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(365 * 24, result.Age); // Should be approximately 8760 hours
        Assert.Equal(365, result.Days);
        Assert.Equal(0, result.Hours);
        Assert.Equal(Levels.URGENT, result.Level);
    }

    [Fact]
    public void CalculateDeviceAge_WithMinuteFractionPrecision_CalculatesAccurately()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // Set treatment time to be exactly 2 hours and 15 minutes ago
        var treatmentTime = currentTime - (2 * 60 * 60 * 1000) - (15 * 60 * 1000);
        var treatments = new List<Treatment>
        {
            new() { EventType = "Test Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 1,
            Warn = 5,
            Urgent = 10,
            Display = "hours",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(2, result.Age); // Should be 2 full hours
        Assert.Equal(15, result.MinFractions); // Should be 15 minutes
    }

    #endregion

    #region Timezone and Date Boundary Tests

    [Fact]
    public void CalculateDeviceAge_AtMidnight_CalculatesCorrectly()
    {
        // Arrange
        var midnightTime = new DateTimeOffset(
            2024,
            1,
            1,
            0,
            0,
            0,
            TimeSpan.Zero
        ).ToUnixTimeMilliseconds();
        var treatmentTime = midnightTime - (5 * 60 * 60 * 1000); // 5 hours before midnight
        var treatments = new List<Treatment>
        {
            new() { EventType = "Test Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 2,
            Warn = 4,
            Urgent = 8,
            Display = "hours",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, midnightTime, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(5, result.Age);
    }

    [Fact]
    public void CalculateDeviceAge_AcrossMonthBoundary_CalculatesCorrectly()
    {
        // Arrange
        var endOfMonth = new DateTimeOffset(
            2024,
            1,
            31,
            23,
            59,
            59,
            TimeSpan.Zero
        ).ToUnixTimeMilliseconds();
        var startOfMonth = new DateTimeOffset(
            2024,
            1,
            1,
            0,
            0,
            0,
            TimeSpan.Zero
        ).ToUnixTimeMilliseconds();
        var treatments = new List<Treatment>
        {
            new() { EventType = "Test Change", Mills = startOfMonth },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 100,
            Warn = 500,
            Urgent = 1000,
            Display = "days",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, endOfMonth, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.True(result.Age > 700); // Should be more than 30 days * 24 hours
        Assert.True(result.Days >= 30); // Should be at least 30 days
    }

    [Fact]
    public void CalculateDeviceAge_AcrossLeapYear_CalculatesCorrectly()
    {
        // Arrange - February 29, 2024 (leap year) to March 1, 2024
        var leapDayEnd = new DateTimeOffset(
            2024,
            3,
            1,
            0,
            0,
            0,
            TimeSpan.Zero
        ).ToUnixTimeMilliseconds();
        var leapDayStart = new DateTimeOffset(
            2024,
            2,
            29,
            0,
            0,
            0,
            TimeSpan.Zero
        ).ToUnixTimeMilliseconds();
        var treatments = new List<Treatment>
        {
            new() { EventType = "Test Change", Mills = leapDayStart },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 12,
            Warn = 24,
            Urgent = 48,
            Display = "hours",
            EnableAlerts = false,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, leapDayEnd, preferences);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(24, result.Age); // Should be exactly 24 hours
        Assert.Equal(1, result.Days);
        Assert.Equal(0, result.Hours);
    }

    #endregion

    #region Levels Class Tests

    [Fact]
    public void Levels_ToDisplay_ReturnsCorrectDisplayText()
    {
        // Arrange & Act & Assert
        Assert.Equal("Urgent", Levels.ToDisplay(Levels.URGENT));
        Assert.Equal("Warning", Levels.ToDisplay(Levels.WARN));
        Assert.Equal("Info", Levels.ToDisplay(Levels.INFO));
        Assert.Equal("Low", Levels.ToDisplay(Levels.LOW));
        Assert.Equal("Lowest", Levels.ToDisplay(Levels.LOWEST));
        Assert.Equal("None", Levels.ToDisplay(Levels.NONE));
        Assert.Equal("Unknown", Levels.ToDisplay(999)); // Invalid level
    }

    [Fact]
    public void Levels_ToLowerCase_ReturnsCorrectLowercaseText()
    {
        // Arrange & Act & Assert
        Assert.Equal("urgent", Levels.ToLowerCase(Levels.URGENT));
        Assert.Equal("warning", Levels.ToLowerCase(Levels.WARN)); // Note: Returns "warning", not "warn" for legacy compatibility
        Assert.Equal("info", Levels.ToLowerCase(Levels.INFO));
        Assert.Equal("low", Levels.ToLowerCase(Levels.LOW));
        Assert.Equal("lowest", Levels.ToLowerCase(Levels.LOWEST));
        Assert.Equal("none", Levels.ToLowerCase(Levels.NONE));
        Assert.Equal("unknown", Levels.ToLowerCase(999)); // Invalid level
    }

    [Fact]
    public void Levels_Constants_HaveCorrectValues()
    {
        // Arrange & Act & Assert - Verify exact legacy compatibility values
        Assert.Equal(2, Levels.URGENT);
        Assert.Equal(1, Levels.WARN);
        Assert.Equal(0, Levels.INFO);
        Assert.Equal(-1, Levels.LOW);
        Assert.Equal(-2, Levels.LOWEST);
        Assert.Equal(-3, Levels.NONE);
    }

    #endregion

    #region Parity Tests

    [Fact]
    [Parity]
    public void CalculateDeviceAge_LegacyCompatibility_With1To1Behavior()
    {
        // Arrange - This test ensures 1:1 parity with legacy JavaScript implementation
        var currentTime = new DateTimeOffset(
            2024,
            1,
            15,
            12,
            0,
            0,
            TimeSpan.Zero
        ).ToUnixTimeMilliseconds();
        var treatmentTime = new DateTimeOffset(
            2024,
            1,
            14,
            10,
            30,
            0,
            TimeSpan.Zero
        ).ToUnixTimeMilliseconds(); // 25.5 hours ago
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Test Change",
                Mills = treatmentTime,
                Notes = "Legacy compatibility test",
            },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 24,
            Warn = 48,
            Urgent = 72,
            Display = "hours",
            EnableAlerts = true,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert - Verify exact legacy behavior
        Assert.True(result.Found);
        Assert.Equal(25, result.Age); // Should floor to 25 hours (25.5 -> 25)
        Assert.Equal(1, result.Days);
        Assert.Equal(1, result.Hours);
        Assert.Equal(30, result.MinFractions); // Should be 30 minutes remainder
        Assert.Equal(Levels.INFO, result.Level); // 25 >= 24 (info threshold)
        Assert.Equal("25h", result.Display);
        Assert.Equal(treatmentTime, result.TreatmentDate);
        Assert.Equal("Legacy compatibility test", result.Notes);
        Assert.Null(result.Notification); // Should be null because 25 != 24 (exact threshold)
    }

    [Fact]
    [Parity]
    public void CalculateDeviceAge_LegacyCompatibility_NotificationTimingWindow()
    {
        // Arrange - Test the exact 20-minute notification window from legacy code
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatmentTime = currentTime - (40 * 60 * 60 * 1000) - (15 * 60 * 1000); // 40 hours 15 minutes ago
        var treatments = new List<Treatment>
        {
            new() { EventType = "Test Change", Mills = treatmentTime },
        };
        var preferences = new DeviceAgePreferences
        {
            Info = 20,
            Warn = 40,
            Urgent = 60,
            Display = "hours",
            EnableAlerts = true,
        };

        // Act
        var result = _service.CalculateDeviceAge(treatments, currentTime, preferences);

        // Assert - Should create notification because age matches threshold and within 20-minute window
        Assert.Equal(40, result.Age);
        Assert.Equal(15, result.MinFractions); // Should be 15 minutes (within 20-minute window)
        Assert.NotNull(result.Notification); // Should create notification
        Assert.Equal("Test device age 40 hours", result.Notification.Title);
    }

    #endregion
}

/// <summary>
/// Test-specific concrete implementation of BaseDeviceAgeService for testing the abstract base class
/// </summary>
internal class TestDeviceAgeService : BaseDeviceAgeService
{
    protected override IEnumerable<Treatment> GetValidTreatments(List<Treatment> treatments)
    {
        // Only accept treatments with "Test Change" event type for testing
        return treatments.Where(t =>
            string.Equals(t.EventType, "Test Change", StringComparison.OrdinalIgnoreCase)
        );
    }

    protected override string GetNotificationGroup()
    {
        return "TEST";
    }

    protected override (string urgent, string warn, string info) GetNotificationMessages()
    {
        return (
            urgent: "Test device urgent message",
            warn: "Test device warn message",
            info: "Test device info message"
        );
    }

    protected override string GetDeviceLabel()
    {
        return "Test device";
    }
}
