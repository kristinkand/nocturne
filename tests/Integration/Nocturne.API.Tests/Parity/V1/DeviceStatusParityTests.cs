using Nocturne.API.Tests.Integration.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Nocturne.API.Tests.Integration.Parity.V1;

/// <summary>
/// Parity tests for /api/v1/devicestatus endpoints.
/// Covers: GET, POST, DELETE/{id}, DELETE (bulk), GET/devicestatus.json
/// </summary>
public class DeviceStatusParityTests : ParityTestBase
{
    public DeviceStatusParityTests(ParityTestFixture fixture, ITestOutputHelper output)
        : base(fixture, output) { }

    #region GET /api/v1/devicestatus

    [Fact]
    public async Task GetDeviceStatus_Empty_ReturnsSameShape()
    {
        await AssertGetParityAsync("/api/v1/devicestatus");
    }

    [Fact]
    public async Task GetDeviceStatus_WithData_ReturnsSameShape()
    {
        var statuses = new[]
        {
            TestDataFactory.CreateDeviceStatus(device: "openaps://phone-1"),
            TestDataFactory.CreateDeviceStatus(
                device: "openaps://phone-2",
                timestamp: TestTimeProvider.GetTestTime().AddMinutes(-5))
        };
        await SeedDeviceStatusAsync(statuses);

        await AssertGetParityAsync("/api/v1/devicestatus");
    }

    [Fact]
    public async Task GetDeviceStatus_WithCount_ReturnsSameShape()
    {
        var statuses = Enumerable.Range(0, 10)
            .Select(i => TestDataFactory.CreateDeviceStatus(
                timestamp: TestTimeProvider.GetTestTime().AddMinutes(-i * 5)))
            .ToArray();
        await SeedDeviceStatusAsync(statuses);

        await AssertGetParityAsync("/api/v1/devicestatus?count=3");
    }

    [Fact]
    public async Task GetDeviceStatus_WithFindFilter_ReturnsSameShape()
    {
        var statuses = new[]
        {
            TestDataFactory.CreateDeviceStatus(device: "loop://iPhone"),
            TestDataFactory.CreateDeviceStatus(device: "openaps://rpi"),
            TestDataFactory.CreateDeviceStatus(device: "loop://iPhone")
        };
        await SeedDeviceStatusAsync(statuses);

        await AssertGetParityAsync("/api/v1/devicestatus?find[device]=loop://iPhone");
    }

    #endregion

    #region GET /api/v1/devicestatus.json

    [Fact]
    public async Task GetDeviceStatusJson_Empty_ReturnsSameShape()
    {
        await AssertGetParityAsync("/api/v1/devicestatus.json");
    }

    [Fact]
    public async Task GetDeviceStatusJson_WithData_ReturnsSameShape()
    {
        var status = TestDataFactory.CreateDeviceStatus();
        await SeedDeviceStatusAsync(status);

        await AssertGetParityAsync("/api/v1/devicestatus.json");
    }

    #endregion

    #region POST /api/v1/devicestatus

    [Fact]
    public async Task PostDeviceStatus_Simple_ReturnsSameShape()
    {
        var status = new
        {
            device = "openaps://test-device",
            created_at = TestTimeProvider.GetTestTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            uploaderBattery = 85
        };

        await AssertPostParityAsync("/api/v1/devicestatus", status);
    }

    [Fact]
    public async Task PostDeviceStatus_WithPump_ReturnsSameShape()
    {
        var status = new
        {
            device = "openaps://test-rig",
            created_at = TestTimeProvider.GetTestTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            pump = new
            {
                battery = new { percent = 75 },
                reservoir = 120.5,
                clock = TestTimeProvider.GetTestTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
            }
        };

        await AssertPostParityAsync("/api/v1/devicestatus", status);
    }

    [Fact]
    public async Task PostDeviceStatus_WithOpenAPS_ReturnsSameShape()
    {
        var status = new
        {
            device = "openaps://myopenaps",
            created_at = TestTimeProvider.GetTestTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            openaps = new
            {
                enacted = new
                {
                    bg = 120,
                    eventualBG = 110,
                    reason = "COB: 0, Dev: -10, BGI: -2, ISF: 50"
                }
            }
        };

        await AssertPostParityAsync("/api/v1/devicestatus", status);
    }

    [Fact]
    public async Task PostDeviceStatus_WithLoop_ReturnsSameShape()
    {
        var status = new
        {
            device = "loop://iPhone",
            created_at = TestTimeProvider.GetTestTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            loop = new
            {
                predicted = new
                {
                    startDate = TestTimeProvider.GetTestTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    values = new[] { 120, 118, 115, 112, 110 }
                },
                enacted = new
                {
                    timestamp = TestTimeProvider.GetTestTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    rate = 0.5,
                    duration = 30
                }
            }
        };

        await AssertPostParityAsync("/api/v1/devicestatus", status);
    }

    #endregion

    #region DELETE /api/v1/devicestatus

    [Fact]
    public async Task DeleteDeviceStatus_Bulk_ReturnsSameShape()
    {
        var statuses = new[]
        {
            TestDataFactory.CreateDeviceStatus(device: "test://delete-me"),
            TestDataFactory.CreateDeviceStatus(device: "test://delete-me")
        };
        await SeedDeviceStatusAsync(statuses);

        await AssertDeleteParityAsync("/api/v1/devicestatus?find[device]=test://delete-me", ComparisonOptions.Default.WithIgnoredFields("connection", "result.n", "n"));
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task PostDeviceStatus_MissingDevice_ReturnsSameShape()
    {
        var status = new
        {
            created_at = TestTimeProvider.GetTestTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            uploaderBattery = 50
        };

        await AssertPostParityAsync("/api/v1/devicestatus", status);
    }

    #endregion
}
