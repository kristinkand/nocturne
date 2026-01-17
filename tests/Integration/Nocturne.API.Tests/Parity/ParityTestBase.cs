using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Nocturne.API.Tests.Integration.Infrastructure;
using Nocturne.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace Nocturne.API.Tests.Integration.Parity;

/// <summary>
/// Base class for API parity tests.
/// Provides helpers for seeding identical data to both systems and comparing responses.
///
/// Key behaviors:
/// - Seeds same data to both Nightscout and Nocturne
/// - Compares responses with strict null vs missing handling
/// - Ignores field order (JSON comparison is unordered)
/// - Handles dynamic fields (IDs, timestamps) appropriately
/// </summary>
[Collection("Parity")]
[Trait("Category", "Parity")]
[Parity]
public abstract class ParityTestBase : IAsyncLifetime
{
    protected readonly ParityTestFixture Fixture;
    protected readonly ITestOutputHelper Output;
    protected readonly ResponseComparer Comparer;

    protected HttpClient NightscoutClient => Fixture.NightscoutClient;
    protected HttpClient NocturneClient => Fixture.NocturneClient;

    protected ParityTestBase(ParityTestFixture fixture, ITestOutputHelper output)
    {
        Fixture = fixture;
        Output = output;
        Comparer = new ResponseComparer(GetComparisonOptions());
    }

    /// <summary>
    /// Override to customize comparison options per test class
    /// </summary>
    protected virtual ComparisonOptions GetComparisonOptions() => ComparisonOptions.Default;

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual async Task DisposeAsync()
    {
        await Fixture.CleanupDataAsync();
    }

    #region Data Seeding Helpers

    /// <summary>
    /// Seeds entries to both Nightscout and Nocturne.
    /// Uses TestDataFactory for consistent data generation.
    /// </summary>
    protected async Task SeedEntriesAsync(params Entry[] entries)
    {
        foreach (var entry in entries)
        {
            // Convert to anonymous object for Nightscout (it expects specific field names)
            var nsEntry = new
            {
                type = entry.Type ?? "sgv",
                sgv = entry.Sgv,
                direction = entry.Direction,
                device = entry.Device,
                date = entry.Mills,
                dateString = entry.DateString,
                noise = entry.Noise,
                filtered = entry.Filtered,
                unfiltered = entry.Unfiltered,
                rssi = entry.Rssi,
                delta = entry.Delta
            };

            var nsResponse = await NightscoutClient.PostAsJsonAsync("/api/v1/entries", new[] { nsEntry });
            nsResponse.EnsureSuccessStatusCode();

            var nocResponse = await NocturneClient.PostAsJsonAsync("/api/v1/entries", new[] { entry });
            nocResponse.EnsureSuccessStatusCode();
        }
    }

    /// <summary>
    /// Seeds treatments to both systems
    /// </summary>
    protected async Task SeedTreatmentsAsync(params Treatment[] treatments)
    {
        foreach (var treatment in treatments)
        {
            var nsTreatment = new
            {
                eventType = treatment.EventType,
                created_at = treatment.CreatedAt,
                insulin = treatment.Insulin,
                carbs = treatment.Carbs,
                notes = treatment.Notes,
                enteredBy = treatment.EnteredBy,
                glucose = treatment.Glucose,
                glucoseType = treatment.GlucoseType,
                duration = treatment.Duration
            };

            var nsResponse = await NightscoutClient.PostAsJsonAsync("/api/v1/treatments", nsTreatment);
            nsResponse.EnsureSuccessStatusCode();

            var nocResponse = await NocturneClient.PostAsJsonAsync("/api/v1/treatments", treatment);
            nocResponse.EnsureSuccessStatusCode();
        }
    }

    /// <summary>
    /// Seeds device status to both systems
    /// </summary>
    protected async Task SeedDeviceStatusAsync(params DeviceStatus[] statuses)
    {
        foreach (var status in statuses)
        {
            var nsStatus = new
            {
                device = status.Device,
                created_at = status.CreatedAt,
                uploaderBattery = status.UploaderBattery
            };

            var nsResponse = await NightscoutClient.PostAsJsonAsync("/api/v1/devicestatus", nsStatus);
            nsResponse.EnsureSuccessStatusCode();

            var nocResponse = await NocturneClient.PostAsJsonAsync("/api/v1/devicestatus", status);
            nocResponse.EnsureSuccessStatusCode();
        }
    }

    /// <summary>
    /// Creates and seeds a sequence of test entries using TestDataFactory
    /// </summary>
    protected async Task SeedEntrySequenceAsync(int count = 5, int intervalMinutes = 5)
    {
        var entries = TestDataFactory.CreateEntrySequence(count, intervalMinutes);
        await SeedEntriesAsync(entries);
    }

    #endregion

    #region Parity Assertion Helpers

    /// <summary>
    /// Asserts that GET requests to both systems return equivalent responses
    /// </summary>
    protected async Task AssertGetParityAsync(
        string path,
        ComparisonOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Output.WriteLine($"Testing GET {path}");

        var nsTask = NightscoutClient.GetAsync(path, cancellationToken);
        var nocTask = NocturneClient.GetAsync(path, cancellationToken);

        await Task.WhenAll(nsTask, nocTask);

        var nsResponse = await nsTask;
        var nocResponse = await nocTask;

        await AssertResponseParityAsync(nsResponse, nocResponse, $"GET {path}", options, cancellationToken);
    }

    /// <summary>
    /// Asserts that POST requests to both systems return equivalent responses
    /// </summary>
    protected async Task AssertPostParityAsync<T>(
        string path,
        T body,
        ComparisonOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Output.WriteLine($"Testing POST {path}");

        var nsTask = NightscoutClient.PostAsJsonAsync(path, body, cancellationToken);
        var nocTask = NocturneClient.PostAsJsonAsync(path, body, cancellationToken);

        await Task.WhenAll(nsTask, nocTask);

        var nsResponse = await nsTask;
        var nocResponse = await nocTask;

        await AssertResponseParityAsync(nsResponse, nocResponse, $"POST {path}", options, cancellationToken);
    }

    /// <summary>
    /// Asserts that PUT requests to both systems return equivalent responses
    /// </summary>
    protected async Task AssertPutParityAsync<T>(
        string path,
        T body,
        ComparisonOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Output.WriteLine($"Testing PUT {path}");

        var nsTask = NightscoutClient.PutAsJsonAsync(path, body, cancellationToken);
        var nocTask = NocturneClient.PutAsJsonAsync(path, body, cancellationToken);

        await Task.WhenAll(nsTask, nocTask);

        var nsResponse = await nsTask;
        var nocResponse = await nocTask;

        await AssertResponseParityAsync(nsResponse, nocResponse, $"PUT {path}", options, cancellationToken);
    }

    /// <summary>
    /// Asserts that DELETE requests to both systems return equivalent responses
    /// </summary>
    protected async Task AssertDeleteParityAsync(
        string path,
        ComparisonOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Output.WriteLine($"Testing DELETE {path}");

        var nsTask = NightscoutClient.DeleteAsync(path, cancellationToken);
        var nocTask = NocturneClient.DeleteAsync(path, cancellationToken);

        await Task.WhenAll(nsTask, nocTask);

        var nsResponse = await nsTask;
        var nocResponse = await nocTask;

        await AssertResponseParityAsync(nsResponse, nocResponse, $"DELETE {path}", options, cancellationToken);
    }

    /// <summary>
    /// Asserts parity for arbitrary HTTP requests with headers
    /// </summary>
    protected async Task AssertParityAsync(
        HttpMethod method,
        string path,
        HttpContent? content = null,
        Dictionary<string, string>? headers = null,
        ComparisonOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Output.WriteLine($"Testing {method} {path}");

        var nsRequest = CreateRequest(method, path, content, headers);
        var nocRequest = CreateRequest(method, path, content, headers);

        var nsTask = NightscoutClient.SendAsync(nsRequest, cancellationToken);
        var nocTask = NocturneClient.SendAsync(nocRequest, cancellationToken);

        await Task.WhenAll(nsTask, nocTask);

        var nsResponse = await nsTask;
        var nocResponse = await nocTask;

        await AssertResponseParityAsync(nsResponse, nocResponse, $"{method} {path}", options, cancellationToken);
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        HttpContent? content,
        Dictionary<string, string>? headers)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };

        if (headers != null)
        {
            foreach (var (key, value) in headers)
            {
                request.Headers.TryAddWithoutValidation(key, value);
            }
        }

        return request;
    }

    private async Task AssertResponseParityAsync(
        HttpResponseMessage nsResponse,
        HttpResponseMessage nocResponse,
        string context,
        ComparisonOptions? options,
        CancellationToken cancellationToken)
    {
        var comparer = options != null ? new ResponseComparer(options) : Comparer;
        var result = await comparer.CompareAsync(nsResponse, nocResponse, context, cancellationToken);

        if (!result.IsMatch)
        {
            Output.WriteLine(result.ToString());

            // Output raw responses for debugging
            Output.WriteLine("--- Nightscout Response ---");
            Output.WriteLine($"Status: {(int)nsResponse.StatusCode} {nsResponse.StatusCode}");
            Output.WriteLine(await nsResponse.Content.ReadAsStringAsync(cancellationToken));

            Output.WriteLine("--- Nocturne Response ---");
            Output.WriteLine($"Status: {(int)nocResponse.StatusCode} {nocResponse.StatusCode}");
            Output.WriteLine(await nocResponse.Content.ReadAsStringAsync(cancellationToken));
        }

        result.IsMatch.Should().BeTrue(result.ToString());
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Logs a message to the test output
    /// </summary>
    protected void Log(string message) => Output.WriteLine(message);

    /// <summary>
    /// Gets a response from both systems for manual inspection
    /// </summary>
    protected async Task<(HttpResponseMessage Nightscout, HttpResponseMessage Nocturne)> GetBothResponsesAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var nsTask = NightscoutClient.GetAsync(path, cancellationToken);
        var nocTask = NocturneClient.GetAsync(path, cancellationToken);

        await Task.WhenAll(nsTask, nocTask);

        return (await nsTask, await nocTask);
    }

    #endregion
}
