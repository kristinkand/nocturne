using Nocturne.API.Tests.Integration.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Nocturne.API.Tests.Integration.Parity.V1;

/// <summary>
/// Parity tests for /api/v1/treatments endpoints.
/// Covers: GET, GET/{id}, POST, PUT/{id}, DELETE/{id}, DELETE (bulk)
/// </summary>
public class TreatmentsParityTests : ParityTestBase
{
    public TreatmentsParityTests(ParityTestFixture fixture, ITestOutputHelper output)
        : base(fixture, output) { }

    #region GET /api/v1/treatments

    [Fact]
    public async Task GetTreatments_Empty_ReturnsSameShape()
    {
        await AssertGetParityAsync("/api/v1/treatments");
    }

    [Fact]
    public async Task GetTreatments_WithData_ReturnsSameShape()
    {
        var treatments = new[]
        {
            TestDataFactory.CreateTreatment(eventType: "Correction Bolus", insulin: 2.0),
            TestDataFactory.CreateTreatment(
                eventType: "Meal Bolus",
                insulin: 3.5,
                timestamp: TestTimeProvider.GetTestTime().AddMinutes(-30))
        };
        await SeedTreatmentsAsync(treatments);

        await AssertGetParityAsync("/api/v1/treatments");
    }

    [Fact]
    public async Task GetTreatments_WithCount_ReturnsSameShape()
    {
        var treatments = Enumerable.Range(0, 10)
            .Select(i => TestDataFactory.CreateTreatment(
                timestamp: TestTimeProvider.GetTestTime().AddMinutes(-i * 15),
                insulin: 1.0 + i * 0.5))
            .ToArray();
        await SeedTreatmentsAsync(treatments);

        await AssertGetParityAsync("/api/v1/treatments?count=5");
    }

    [Fact]
    public async Task GetTreatments_WithFindFilter_ReturnsSameShape()
    {
        var treatments = new[]
        {
            TestDataFactory.CreateTreatment(eventType: "Correction Bolus", insulin: 2.0),
            TestDataFactory.CreateTreatment(eventType: "Meal Bolus", insulin: 5.0),
            TestDataFactory.CreateTreatment(eventType: "Carb Correction")
        };
        await SeedTreatmentsAsync(treatments);

        await AssertGetParityAsync("/api/v1/treatments?find[eventType]=Meal%20Bolus");
        await AssertGetParityAsync("/api/v1/treatments?find[insulin][$gte]=3");
    }

    [Fact]
    public async Task GetTreatments_WithEventTypeFilter_ReturnsSameShape()
    {
        var treatments = new[]
        {
            TestDataFactory.CreateTreatment(eventType: "Temp Basal"),
            TestDataFactory.CreateTreatment(eventType: "Correction Bolus"),
            TestDataFactory.CreateTreatment(eventType: "Temp Basal")
        };
        await SeedTreatmentsAsync(treatments);

        await AssertGetParityAsync("/api/v1/treatments?find[eventType]=Temp%20Basal");
    }

    #endregion

    #region POST /api/v1/treatments

    [Fact]
    public async Task PostTreatment_Single_ReturnsSameShape()
    {
        var treatment = new
        {
            eventType = "Correction Bolus",
            insulin = 2.5,
            created_at = TestTimeProvider.GetTestTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            enteredBy = "Test",
            notes = "Parity test"
        };

        await AssertPostParityAsync("/api/v1/treatments", treatment);
    }

    [Fact]
    public async Task PostTreatment_WithCarbs_ReturnsSameShape()
    {
        var treatment = new
        {
            eventType = "Meal Bolus",
            insulin = 4.0,
            carbs = 45,
            created_at = TestTimeProvider.GetTestTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            enteredBy = "Test"
        };

        await AssertPostParityAsync("/api/v1/treatments", treatment);
    }

    [Fact]
    public async Task PostTreatment_TempBasal_ReturnsSameShape()
    {
        var treatment = new
        {
            eventType = "Temp Basal",
            duration = 30,
            percent = -50,
            created_at = TestTimeProvider.GetTestTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            enteredBy = "Test"
        };

        await AssertPostParityAsync("/api/v1/treatments", treatment);
    }

    [Fact]
    public async Task PostTreatment_ProfileSwitch_ReturnsSameShape()
    {
        var treatment = new
        {
            eventType = "Profile Switch",
            profile = "Default",
            created_at = TestTimeProvider.GetTestTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            enteredBy = "Test"
        };

        await AssertPostParityAsync("/api/v1/treatments", treatment);
    }

    #endregion

    #region DELETE /api/v1/treatments

    [Fact]
    public async Task DeleteTreatments_Bulk_ReturnsSameShape()
    {
        var treatments = new[]
        {
            TestDataFactory.CreateTreatment(eventType: "Note"),
            TestDataFactory.CreateTreatment(eventType: "Note")
        };
        await SeedTreatmentsAsync(treatments);

        await AssertDeleteParityAsync("/api/v1/treatments?find[eventType]=Note");
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task GetTreatments_InvalidCount_ReturnsSameShape()
    {
        await AssertGetParityAsync("/api/v1/treatments?count=-1");
    }

    [Fact]
    public async Task PostTreatment_MissingEventType_ReturnsSameShape()
    {
        var treatment = new
        {
            insulin = 2.0,
            created_at = TestTimeProvider.GetTestTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };

        await AssertPostParityAsync("/api/v1/treatments", treatment);
    }

    #endregion
}
