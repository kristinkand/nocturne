using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Services;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services;

/// <summary>
/// Tests for COB (Carbs on Board) functionality with 1:1 legacy compatibility
/// Based on legacy cob.test.js
/// </summary>
public class CobTests
{
    private readonly Mock<ILogger<Nocturne.API.Services.CobService>> _mockLogger;
    private readonly Mock<IIobService> _mockIobService;
    private readonly Nocturne.API.Services.CobService _cobService;

    public CobTests()
    {
        _mockLogger = new Mock<ILogger<Nocturne.API.Services.CobService>>();
        _mockIobService = new Mock<IIobService>();
        _cobService = new Nocturne.API.Services.CobService(
            _mockLogger.Object,
            _mockIobService.Object
        );
    }

    [Fact]
    public void CobTotal_ShouldCalculateFromMultipleTreatments()
    {
        // Arrange
        var cobProfile = CreateDefaultProfile();
        var profileService = new TestProfileService(cobProfile);
        var firstTreatmentTime = new DateTime(2015, 5, 29, 2, 3, 48, 827, DateTimeKind.Utc);
        var secondTreatmentTime = new DateTime(2015, 5, 29, 3, 45, 10, 670, DateTimeKind.Utc);

        var treatments = new List<Treatment>
        {
            new()
            {
                Carbs = 100,
                Mills = ((DateTimeOffset)firstTreatmentTime).ToUnixTimeMilliseconds(),
            },
            new()
            {
                Carbs = 10,
                Mills = ((DateTimeOffset)secondTreatmentTime).ToUnixTimeMilliseconds(),
            },
        };

        // Test different time points
        var after100 = ((DateTimeOffset)firstTreatmentTime.AddSeconds(1)).ToUnixTimeMilliseconds();
        var before10 = ((DateTimeOffset)secondTreatmentTime).ToUnixTimeMilliseconds();
        var after10 = ((DateTimeOffset)secondTreatmentTime.AddSeconds(1)).ToUnixTimeMilliseconds();

        // Act
        var result1 = _cobService.CobTotal(
            treatments,
            Array.Empty<DeviceStatus>().ToList(),
            profileService,
            after100
        );
        var result2 = _cobService.CobTotal(
            treatments,
            Array.Empty<DeviceStatus>().ToList(),
            profileService,
            before10
        );
        var result3 = _cobService.CobTotal(
            treatments,
            Array.Empty<DeviceStatus>().ToList(),
            profileService,
            after10
        );

        // Assert
        Assert.Equal(100, result1.Cob);
        Assert.Equal(59, Math.Round(result2.Cob));
        Assert.Equal(69, Math.Round(result3.Cob));
    }

    [Fact]
    public void CobTotal_ShouldCalculateFromSingleTreatment()
    {
        // Arrange
        var cobProfile = CreateDefaultProfile();
        var profileService = new TestProfileService(cobProfile);
        var treatmentTime = new DateTime(2015, 5, 29, 4, 40, 40, 174, DateTimeKind.Utc);

        var treatments = new List<Treatment>
        {
            new() { Carbs = 8, Mills = ((DateTimeOffset)treatmentTime).ToUnixTimeMilliseconds() },
        };

        // Test different time points
        var rightAfter = ((DateTimeOffset)treatmentTime.AddMinutes(1)).ToUnixTimeMilliseconds();
        var later1 = ((DateTimeOffset)treatmentTime.AddMinutes(24)).ToUnixTimeMilliseconds();
        var later2 = ((DateTimeOffset)treatmentTime.AddMinutes(40)).ToUnixTimeMilliseconds();
        var later3 = ((DateTimeOffset)treatmentTime.AddMinutes(70)).ToUnixTimeMilliseconds();
        var later4 = ((DateTimeOffset)treatmentTime.AddMinutes(130)).ToUnixTimeMilliseconds();

        // Act
        var result1 = _cobService.CobTotal(
            treatments,
            Array.Empty<DeviceStatus>().ToList(),
            profileService,
            rightAfter
        );
        var result2 = _cobService.CobTotal(
            treatments,
            Array.Empty<DeviceStatus>().ToList(),
            profileService,
            later1
        );
        var result3 = _cobService.CobTotal(
            treatments,
            Array.Empty<DeviceStatus>().ToList(),
            profileService,
            later2
        );
        var result4 = _cobService.CobTotal(
            treatments,
            Array.Empty<DeviceStatus>().ToList(),
            profileService,
            later3
        );
        var result5 = _cobService.CobTotal(
            treatments,
            Array.Empty<DeviceStatus>().ToList(),
            profileService,
            later4
        );

        // Assert
        Assert.Equal(8, result1.Cob);
        Assert.Equal(6, result2.Cob);
        Assert.Equal(0, result3.Cob);
        Assert.Equal(0, result4.Cob);
        Assert.Equal(0, result5.Cob);
    }

    [Fact]
    public void CobTotal_ShouldHandleZeroCarbs()
    {
        // Arrange
        var cobProfile = CreateDefaultProfile();
        var profileService = new TestProfileService(cobProfile);
        var treatments = new List<Treatment>
        {
            new() { Carbs = 0, Mills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
        };

        // Act
        var result = _cobService.CobTotal(
            treatments,
            Array.Empty<DeviceStatus>().ToList(),
            profileService,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        );

        // Assert
        Assert.Equal(0, result.Cob);
    }

    [Fact]
    public void CobTotal_ShouldIgnoreNullCarbs()
    {
        // Arrange
        var cobProfile = CreateDefaultProfile();
        var profileService = new TestProfileService(cobProfile);
        var treatments = new List<Treatment>
        {
            new() { Carbs = null, Mills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
        };

        // Act
        var result = _cobService.CobTotal(
            treatments,
            Array.Empty<DeviceStatus>().ToList(),
            profileService,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        );

        // Assert
        Assert.Equal(0, result.Cob);
    }

    [Fact]
    public void CobTotal_ShouldUseDefaultAbsorptionRate()
    {
        // Arrange
        var treatments = new List<Treatment>
        {
            new()
            {
                Carbs = 30,
                Mills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (30 * 60 * 1000), // 30 minutes ago
            },
        };

        // Act (no profile provided, should use defaults)
        var result = _cobService.CobTotal(
            treatments,
            Array.Empty<DeviceStatus>().ToList(),
            null,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        );

        // Assert
        Assert.True(result.Cob > 0);
        Assert.True(result.Cob < 30); // Should have absorbed some carbs
    }

    private static CobProfile CreateDefaultProfile()
    {
        return new CobProfile
        {
            CarbsHr = 30, // 30g carbs absorbed per hour
            Sens = 95, // Insulin sensitivity
            CarbRatio = 18, // Carb ratio
        };
    }

    /// <summary>
    /// Test ProfileService that adapts CobProfile for testing
    /// </summary>
    private class TestProfileService : IProfileService
    {
        private readonly CobProfile _profile;

        public TestProfileService(CobProfile profile)
        {
            _profile = profile;
        }

        public bool HasData() => true;

        public double GetSensitivity(long time, string? specProfile = null) => _profile.Sens;

        public double GetCarbRatio(long time, string? specProfile = null) => _profile.CarbRatio;

        public double GetCarbAbsorptionRate(long time, string? specProfile = null) =>
            _profile.CarbsHr;

        // Unused methods for COB testing
        public void LoadData(List<Profile> profileData) { }

        public void Clear() { }

        public Profile? GetCurrentProfile(long? time = null, string? specProfile = null) => null;

        public string? GetActiveProfileName(long? time = null) => null;

        public List<string> ListBasalProfiles() => new();

        public string? GetUnits(string? specProfile = null) => "mg/dl";

        public string? GetTimezone(string? specProfile = null) => null;

        public double GetValueByTime(long time, string valueType, string? specProfile = null) => 0;

        public double GetDIA(long time, string? specProfile = null) => 4.0;

        public double GetLowBGTarget(long time, string? specProfile = null) => 80;

        public double GetHighBGTarget(long time, string? specProfile = null) => 120;

        public double GetBasalRate(long time, string? specProfile = null) => 1.0;

        public void UpdateTreatments(
            List<Treatment>? profileTreatments = null,
            List<Treatment>? tempBasalTreatments = null,
            List<Treatment>? comboBolusTreatments = null
        ) { }

        public Treatment? GetActiveProfileTreatment(long time) => null;

        public Treatment? GetTempBasalTreatment(long time) => null;

        public Treatment? GetComboBolusTreatment(long time) => null;

        public TempBasalResult GetTempBasal(long time, string? specProfile = null) => new();
    }
}

/// <summary>
/// Profile data for COB calculations
/// </summary>
public class CobProfile
{
    public double CarbsHr { get; set; } = 30.0;
    public double Sens { get; set; } = 95.0;
    public double CarbRatio { get; set; } = 18.0;
}
