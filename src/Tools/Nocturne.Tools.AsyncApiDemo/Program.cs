using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Models;

namespace Nocturne.Demo.AsyncAPI;

/// <summary>
/// Demo client to showcase async API functionality
/// </summary>
public class AsyncApiDemo
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AsyncApiDemo> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public AsyncApiDemo(HttpClient httpClient, ILogger<AsyncApiDemo> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
        };
    }

    /// <summary>
    /// Demonstrates the complete async API workflow
    /// </summary>
    public async Task RunDemoAsync()
    {
        try
        {
            _logger.LogInformation("=== Nocturne Async API Demo ===");

            // Step 1: Submit data asynchronously
            var correlationId = await SubmitDataAsync();
            if (string.IsNullOrEmpty(correlationId))
            {
                _logger.LogError("Failed to submit data, stopping demo");
                return;
            }

            // Step 2: Poll for status updates
            await PollForStatusAsync(correlationId);

            // Step 3: Wait for completion using long polling
            await WaitForCompletionAsync(correlationId);

            _logger.LogInformation("=== Demo Complete ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Demo failed with error");
        }
    }

    /// <summary>
    /// Submit glucose entries asynchronously
    /// </summary>
    private async Task<string?> SubmitDataAsync()
    {
        _logger.LogInformation("Step 1: Submitting glucose entries asynchronously...");

        var entries = new[]
        {
            new Entry
            {
                Type = "sgv",
                Sgv = 120,
                Mills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Direction = "Flat",
                DateString = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            },
            new Entry
            {
                Type = "sgv",
                Sgv = 125,
                Mills = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds(),
                Direction = "FortyFiveUp",
                DateString = DateTimeOffset
                    .UtcNow.AddMinutes(5)
                    .ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            },
            new Entry
            {
                Type = "sgv",
                Sgv = 130,
                Mills = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeMilliseconds(),
                Direction = "SingleUp",
                DateString = DateTimeOffset
                    .UtcNow.AddMinutes(10)
                    .ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            },
        };

        var json = JsonSerializer.Serialize(entries, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync("/api/v1/entries/async", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var asyncResponse = JsonSerializer.Deserialize<AsyncProcessingResponse>(
                    responseContent,
                    _jsonOptions
                );
                _logger.LogInformation("✅ Data submitted successfully!");
                _logger.LogInformation(
                    "   Correlation ID: {CorrelationId}",
                    asyncResponse?.CorrelationId
                );
                _logger.LogInformation("   Status: {Status}", asyncResponse?.Status);
                _logger.LogInformation("   Status URL: {StatusUrl}", asyncResponse?.StatusUrl);
                _logger.LogInformation(
                    "   Estimated completion: {EstimatedCompletion}",
                    asyncResponse?.EstimatedCompletion
                );
                return asyncResponse?.CorrelationId;
            }
            else
            {
                _logger.LogError(
                    "❌ Failed to submit data: {StatusCode} - {Content}",
                    response.StatusCode,
                    responseContent
                );
                return null;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("⚠️ API server not running: {Message}", ex.Message);
            _logger.LogInformation("💡 To test this demo, start the Nocturne API server first:");
            _logger.LogInformation("   cd src/Aspire/Nocturne.Aspire.Host && dotnet run");
            return null;
        }
    }

    /// <summary>
    /// Poll for processing status updates
    /// </summary>
    private async Task PollForStatusAsync(string correlationId)
    {
        _logger.LogInformation("Step 2: Polling for status updates...");

        for (int i = 0; i < 10; i++)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"/api/v1/processing/status/{correlationId}"
                );
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var status = JsonSerializer.Deserialize<ProcessingStatusResponse>(
                        responseContent,
                        _jsonOptions
                    );
                    _logger.LogInformation(
                        "📊 Status Update #{Poll}: {Status} - {Progress}% ({ProcessedCount}/{TotalCount})",
                        i + 1,
                        status?.Status,
                        status?.Progress,
                        status?.ProcessedCount,
                        status?.TotalCount
                    );

                    if (status?.Status is "completed" or "failed")
                    {
                        _logger.LogInformation("✅ Processing {Status}!", status.Status);
                        if (status.Status == "completed" && status.Results != null)
                        {
                            _logger.LogInformation(
                                "📋 Results: {Results}",
                                JsonSerializer.Serialize(status.Results, _jsonOptions)
                            );
                        }
                        return;
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("⚠️ Processing status not found (may have expired)");
                    return;
                }
                else
                {
                    _logger.LogWarning(
                        "⚠️ Failed to get status: {StatusCode}",
                        response.StatusCode
                    );
                }

                await Task.Delay(1000); // Wait 1 second between polls
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("⚠️ Network error during polling: {Message}", ex.Message);
                return;
            }
        }

        _logger.LogInformation("⏰ Polling timeout reached");
    }

    /// <summary>
    /// Wait for completion using long polling
    /// </summary>
    private async Task WaitForCompletionAsync(string correlationId)
    {
        _logger.LogInformation("Step 3: Using long polling to wait for completion...");

        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/v1/processing/status/{correlationId}/wait?timeoutSeconds=30"
            );
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var status = JsonSerializer.Deserialize<ProcessingStatusResponse>(
                    responseContent,
                    _jsonOptions
                );
                _logger.LogInformation(
                    "🎉 Long polling completed! Status: {Status}",
                    status?.Status
                );

                if (status?.CompletedAt.HasValue == true)
                {
                    var duration = status.CompletedAt.Value - status.StartedAt;
                    _logger.LogInformation(
                        "⏱️ Total processing time: {Duration}ms",
                        duration.TotalMilliseconds
                    );
                }

                if (status?.Results != null)
                {
                    _logger.LogInformation(
                        "📋 Final Results: {Results}",
                        JsonSerializer.Serialize(status.Results, _jsonOptions)
                    );
                }

                if (status?.Errors?.Any() == true)
                {
                    _logger.LogWarning(
                        "⚠️ Errors occurred: {Errors}",
                        string.Join(", ", status.Errors)
                    );
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
            {
                _logger.LogWarning("⏰ Long polling timed out");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("⚠️ Processing status not found (may have expired)");
            }
            else
            {
                _logger.LogWarning(
                    "⚠️ Long polling failed: {StatusCode} - {Content}",
                    response.StatusCode,
                    responseContent
                );
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("⚠️ Network error during long polling: {Message}", ex.Message);
        }
    }
}

/// <summary>
/// Console application to run the demo
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddHttpClient<AsyncApiDemo>(client =>
                {
                    client.BaseAddress = new Uri("https://localhost:1612"); // Default Aspire HTTPS port
                    client.Timeout = TimeSpan.FromMinutes(2);
                });
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            })
            .Build();

        var demo = host.Services.GetRequiredService<AsyncApiDemo>();
        await demo.RunDemoAsync();
    }
}
