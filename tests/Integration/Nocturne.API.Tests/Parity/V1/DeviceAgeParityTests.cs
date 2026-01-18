using System.Net.Http.Json;
using Nocturne.API.Tests.Integration.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Nocturne.API.Tests.Integration.Parity.V1;

/// <summary>
/// Parity tests for /api/v1/deviceage/* (legacy device age) endpoints.
/// Covers: GET/cannula, GET/sensor, GET/insulin, GET/battery, GET/all
/// These endpoints report on consumable ages (site changes, sensor insertions, etc.)
/// </summary>
public class DeviceAgeParityTests : ParityTestBase
{
    public DeviceAgeParityTests(ParityTestFixture fixture, ITestOutputHelper output)
        : base(fixture, output) { }

    protected override ComparisonOptions GetComparisonOptions()
    {
        // Age calculations will differ based on current time
        return ComparisonOptions.Default.WithIgnoredFields(
            "age",
            "ageDisplay",
            "hours",
            "minutes",
            "checkTime",
            "lastChange"
        );
    }

    #region Data Setup

    private async Task SeedDeviceAgeTestDataAsync()
    {
        // Seed treatments that represent site/sensor changes
        var treatments = new object[]
        {
            new
            {
                eventType = "Site Change",
                created_at = TestTimeProvider.GetTestTime().AddDays(-2).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                enteredBy = "Test",
                notes = "New infusion site"
            },
            new
            {
                eventType = "Sensor Start",
                created_at = TestTimeProvider.GetTestTime().AddDays(-5).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                enteredBy = "Test",
                notes = "New sensor"
            },
            new
            {
                eventType = "Insulin Change",
                created_at = TestTimeProvider.GetTestTime().AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                enteredBy = "Test",
                notes = "Reservoir change"
            },
            new
            {
                eventType = "Pump Battery Change",
                created_at = TestTimeProvider.GetTestTime().AddDays(-10).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                enteredBy = "Test",
                notes = "New pump battery"
            }
        };

        foreach (var treatment in treatments)
        {
            await NightscoutClient.PostAsJsonAsync("/api/v1/treatments", treatment);
            await NocturneClient.PostAsJsonAsync("/api/v1/treatments", treatment);
        }
    }

    #endregion

    #region GET /api/v1/deviceage/cannula

    [Fact]
    public async Task GetDeviceAgeCannula_Empty_ReturnsSameShape()
    {
        await AssertGetParityAsync("/api/v1/deviceage/cannula");
    }

    [Fact]
    public async Task GetDeviceAgeCannula_WithData_ReturnsSameShape()
    {
        await SeedDeviceAgeTestDataAsync();

        await AssertGetParityAsync("/api/v1/deviceage/cannula");
    }

    #endregion

    #region GET /api/v1/deviceage/sensor

    [Fact]
    public async Task GetDeviceAgeSensor_Empty_ReturnsSameShape()
    {
        await AssertGetParityAsync("/api/v1/deviceage/sensor");
    }

    [Fact]
    public async Task GetDeviceAgeSensor_WithData_ReturnsSameShape()
    {
        await SeedDeviceAgeTestDataAsync();

        await AssertGetParityAsync("/api/v1/deviceage/sensor");
    }

    #endregion

    #region GET /api/v1/deviceage/insulin

    [Fact]
    public async Task GetDeviceAgeInsulin_Empty_ReturnsSameShape()
    {
        await AssertGetParityAsync("/api/v1/deviceage/insulin");
    }

    [Fact]
    public async Task GetDeviceAgeInsulin_WithData_ReturnsSameShape()
    {
        await SeedDeviceAgeTestDataAsync();

        await AssertGetParityAsync("/api/v1/deviceage/insulin");
    }

    #endregion

    #region GET /api/v1/deviceage/battery

    [Fact]
    public async Task GetDeviceAgeBattery_Empty_ReturnsSameShape()
    {
        await AssertGetParityAsync("/api/v1/deviceage/battery");
    }

    [Fact]
    public async Task GetDeviceAgeBattery_WithData_ReturnsSameShape()
    {
        await SeedDeviceAgeTestDataAsync();

        await AssertGetParityAsync("/api/v1/deviceage/battery");
    }

    #endregion

    #region GET /api/v1/deviceage/all

    [Fact]
    public async Task GetDeviceAgeAll_Empty_ReturnsSameShape()
    {
        await AssertGetParityAsync("/api/v1/deviceage/all");
    }

    [Fact]
    public async Task GetDeviceAgeAll_WithData_ReturnsSameShape()
    {
        await SeedDeviceAgeTestDataAsync();

        await AssertGetParityAsync("/api/v1/deviceage/all");
    }

    [Fact]
    public async Task GetDeviceAgeAll_PartialData_ReturnsSameShape()
    {
        // Only seed some device age events
        var treatment = new
        {
            eventType = "Site Change",
            created_at = TestTimeProvider.GetTestTime().AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            enteredBy = "Test"
        };

        await NightscoutClient.PostAsJsonAsync("/api/v1/treatments", treatment);
        await NocturneClient.PostAsJsonAsync("/api/v1/treatments", treatment);

        await AssertGetParityAsync("/api/v1/deviceage/all");
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task GetDeviceAge_InvalidType_ReturnsSameShape()
    {
        // This might 404 or return empty - both should match
        await AssertGetParityAsync("/api/v1/deviceage/invalid");
    }

    #endregion
}
