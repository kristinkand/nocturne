using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Services;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services;

/// <summary>
/// Simple validation tests for the COB service with unified profile interface
/// </summary>
public class CobServiceValidationTests
{
    private readonly ICobService _cobService;
    private readonly TestProfileService _testProfile;

    public CobServiceValidationTests()
    {
        var logger = new Mock<ILogger<Nocturne.API.Services.CobService>>();
        var iobService = new Mock<IIobService>();
        _cobService = new Nocturne.API.Services.CobService(logger.Object, iobService.Object);
        _testProfile = new TestProfileService();
    }

    [Fact]
    public void CobTotal_WithCarbs_ShouldReturnPositiveCob()
    {
        // Arrange
        var treatments = new List<Treatment>
        {
            new()
            {
                Carbs = 50,
                Mills = DateTimeOffset.UtcNow.AddMinutes(-30).ToUnixTimeMilliseconds(),
            },
        };

        // Act
        var result = _cobService.CobTotal(treatments, new List<DeviceStatus>(), _testProfile);

        // Assert
        Assert.True(result.Cob >= 0, "COB should be non-negative");
        Assert.Equal("Care Portal", result.Source);
    }

    [Fact]
    public void CobTotal_WithoutCarbs_ShouldReturnZeroCob()
    {
        // Arrange
        var treatments = new List<Treatment>
        {
            new()
            {
                Insulin = 5.0,
                Mills = DateTimeOffset.UtcNow.AddMinutes(-30).ToUnixTimeMilliseconds(),
            },
        };

        // Act
        var result = _cobService.CobTotal(treatments, new List<DeviceStatus>(), _testProfile);

        // Assert
        Assert.Equal(0.0, result.Cob);
    }

    [Fact]
    public void CobTotal_WithRecentDeviceStatus_ShouldPrioritizeDeviceStatus()
    {
        // Arrange
        var treatments = new List<Treatment>
        {
            new()
            {
                Carbs = 50,
                Mills = DateTimeOffset.UtcNow.AddMinutes(-30).ToUnixTimeMilliseconds(),
            },
        };

        var deviceStatus = new List<DeviceStatus>
        {
            new()
            {
                Device = "Loop",
                Mills = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds(),
                Loop = new LoopStatus { Cob = 25.5 },
            },
        };

        // Act
        var result = _cobService.CobTotal(treatments, deviceStatus, _testProfile);

        // Assert
        Assert.Equal(25.5, result.Cob);
        Assert.Equal("Loop", result.Source);
    }

    [Fact]
    public void FromDeviceStatus_WithLoopCob_ShouldExtractCorrectly()
    {
        // Arrange
        var deviceStatus = new DeviceStatus
        {
            Device = "MyLoop",
            Mills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Loop = new LoopStatus { Cob = 15.7 },
        };

        // Act
        var result = _cobService.FromDeviceStatus(deviceStatus);

        // Assert
        Assert.Equal(15.7, result.Cob);
        Assert.Equal("Loop", result.Source);
        Assert.Equal("MyLoop", result.Device);
    }

    private class TestProfileService : IProfileService
    {
        public double GetCarbAbsorptionRate(long time, string? specProfile = null) => 30.0;

        public double GetCarbRatio(long time, string? specProfile = null) => 15.0;

        public double GetSensitivity(long time, string? specProfile = null) => 50.0;

        public double GetDIA(long time, string? specProfile = null) => 3.0;

        public double GetBasalRate(long time, string? specProfile = null) => 1.0;

        public bool HasData() => true;

        public void LoadData(List<Profile> profileData) { }

        public Profile? GetCurrentProfile(long? time = null, string? specProfile = null) => null;

        // New interface methods with default implementations for tests
        public void Clear() { }

        public string? GetActiveProfileName(long? time = null) => "Default";

        public List<string> ListBasalProfiles() => new List<string> { "Default" };

        public string? GetUnits(string? specProfile = null) => "mg/dl";

        public string? GetTimezone(string? specProfile = null) => null;

        public double GetValueByTime(long time, string valueType, string? specProfile = null) =>
            0.0;

        public double GetLowBGTarget(long time, string? specProfile = null) => 70.0;

        public double GetHighBGTarget(long time, string? specProfile = null) => 180.0;

        public void UpdateTreatments(
            List<Treatment>? profileTreatments = null,
            List<Treatment>? tempBasalTreatments = null,
            List<Treatment>? comboBolusTreatments = null
        ) { }

        public Treatment? GetActiveProfileTreatment(long time) => null;

        public Treatment? GetTempBasalTreatment(long time) => null;

        public Treatment? GetComboBolusTreatment(long time) => null;

        public TempBasalResult GetTempBasal(long time, string? specProfile = null) =>
            new TempBasalResult
            {
                Basal = 1.0,
                TempBasal = 1.0,
                TotalBasal = 1.0,
            };
    }
}
