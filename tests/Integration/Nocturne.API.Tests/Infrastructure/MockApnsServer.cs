using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Nocturne.API.Tests.Integration.Infrastructure;

/// <summary>
/// Mock APNS server for testing Loop notifications
/// Captures HTTP requests that would normally go to Apple's APNS servers
/// </summary>
public class MockApnsServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly ConcurrentBag<CapturedApnsRequest> _requests = [];
    private readonly TaskCompletionSource _started = new();

    /// <summary>
    /// Gets the list of captured APNS requests
    /// </summary>
    public IReadOnlyCollection<CapturedApnsRequest> Requests => _requests.ToArray();

    /// <summary>
    /// Gets the base address of the mock server
    /// </summary>
    public Uri BaseAddress { get; }

    /// <summary>
    /// Response to return for APNS requests (defaults to success)
    /// </summary>
    public ApnsMockResponse NextResponse { get; set; } = ApnsMockResponse.Success();

    private MockApnsServer(WebApplication app, int port)
    {
        _app = app;
        BaseAddress = new Uri($"http://localhost:{port}");
    }

    /// <summary>
    /// Creates and starts a new mock APNS server on an available port
    /// </summary>
    public static async Task<MockApnsServer> StartAsync()
    {
        // Find an available port
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{port}");
        builder.Logging.SetMinimumLevel(LogLevel.None);

        var app = builder.Build();
        var server = new MockApnsServer(app, port);

        // Configure APNS endpoint: POST /3/device/{deviceToken}
        app.MapPost(
            "/3/device/{deviceToken}",
            async (HttpContext context, string deviceToken) =>
            {
                var headers = context.Request.Headers.ToDictionary(
                    h => h.Key,
                    h => h.Value.ToString()
                );

                string body;
                using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
                {
                    body = await reader.ReadToEndAsync();
                }

                JsonDocument? payload = null;
                if (!string.IsNullOrEmpty(body))
                {
                    try
                    {
                        payload = JsonDocument.Parse(body);
                    }
                    catch (JsonException)
                    {
                        // Body wasn't valid JSON
                    }
                }

                var request = new CapturedApnsRequest(deviceToken, headers, body, payload);
                server._requests.Add(request);

                var response = server.NextResponse;
                context.Response.StatusCode = response.StatusCode;

                if (!string.IsNullOrEmpty(response.ApnsId))
                {
                    context.Response.Headers["apns-id"] = response.ApnsId;
                }

                if (!string.IsNullOrEmpty(response.Body))
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(response.Body);
                }
            }
        );

        // Start the server
        _ = app.RunAsync();

        // Wait a bit for the server to start
        await Task.Delay(100);

        return server;
    }

    /// <summary>
    /// Clears all captured requests
    /// </summary>
    public void ClearRequests()
    {
        _requests.Clear();
    }

    /// <summary>
    /// Gets the most recent captured request
    /// </summary>
    public CapturedApnsRequest? GetLastRequest()
    {
        return _requests.LastOrDefault();
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Represents a captured APNS HTTP request
/// </summary>
public record CapturedApnsRequest(
    string DeviceToken,
    Dictionary<string, string> Headers,
    string RawBody,
    JsonDocument? Payload
)
{
    /// <summary>
    /// Gets a payload property value as string
    /// </summary>
    public string? GetPayloadProperty(string propertyName)
    {
        if (Payload == null)
            return null;

        if (Payload.RootElement.TryGetProperty(propertyName, out var prop))
        {
            return prop.ValueKind switch
            {
                JsonValueKind.String => prop.GetString(),
                JsonValueKind.Number => prop.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => prop.GetRawText(),
            };
        }

        return null;
    }

    /// <summary>
    /// Gets the 'aps' section of the payload
    /// </summary>
    public JsonElement? GetApsSection()
    {
        if (Payload?.RootElement.TryGetProperty("aps", out var aps) == true)
        {
            return aps;
        }
        return null;
    }

    /// <summary>
    /// Gets the alert message from the aps section
    /// </summary>
    public string? GetAlertMessage()
    {
        var aps = GetApsSection();
        if (aps?.TryGetProperty("alert", out var alert) == true)
        {
            return alert.ValueKind == JsonValueKind.String ? alert.GetString() : alert.GetRawText();
        }
        return null;
    }
}

/// <summary>
/// Configurable mock APNS response
/// </summary>
public class ApnsMockResponse
{
    public int StatusCode { get; init; }
    public string? ApnsId { get; init; }
    public string? Body { get; init; }

    /// <summary>
    /// Creates a successful APNS response
    /// </summary>
    public static ApnsMockResponse Success(string? apnsId = null) =>
        new()
        {
            StatusCode = 200,
            ApnsId = apnsId ?? Guid.NewGuid().ToString(),
        };

    /// <summary>
    /// Creates a failed APNS response with a reason
    /// </summary>
    public static ApnsMockResponse Failure(string reason, int statusCode = 400) =>
        new()
        {
            StatusCode = statusCode,
            ApnsId = Guid.NewGuid().ToString(),
            Body = JsonSerializer.Serialize(new { reason }),
        };

    /// <summary>
    /// Creates an expired device token response
    /// </summary>
    public static ApnsMockResponse ExpiredToken() =>
        Failure("ExpiredProviderToken", 403);

    /// <summary>
    /// Creates a bad device token response
    /// </summary>
    public static ApnsMockResponse BadDeviceToken() =>
        Failure("BadDeviceToken", 400);
}
