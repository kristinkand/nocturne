using System.Security.Cryptography;
using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.MongoDb;

namespace Nocturne.API.Tests.Integration.Infrastructure;

/// <summary>
/// Manages a Nightscout 15.0.3 container with its MongoDB dependency for parity testing.
/// Follows the same lifecycle pattern as SharedContainerState in TestDatabaseFixture.
/// </summary>
public class NightscoutContainer : IAsyncDisposable
{
    private const string NightscoutImage = "nightscout/cgm-remote-monitor:15.0.3";
    private const string ApiSecret = "test-api-secret-12chars";
    private const int NightscoutPort = 1337;

    private MongoDbContainer? _mongoContainer;
    private IContainer? _nightscoutContainer;
    private HttpClient? _httpClient;

    public string BaseUrl { get; private set; } = string.Empty;
    public string MongoConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Pre-computed SHA1 hash of the API secret for authenticated requests
    /// </summary>
    public string ApiSecretHash { get; } = ComputeSha1Hash(ApiSecret);

    /// <summary>
    /// HttpClient configured with the API secret header for authenticated requests
    /// </summary>
    public HttpClient Client => _httpClient ?? throw new InvalidOperationException("Container not started");

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        using var measurement = TestPerformanceTracker.MeasureTest("NightscoutContainer.Start");

        // Start MongoDB first (Nightscout's backend)
        _mongoContainer = new MongoDbBuilder()
            .WithImage("mongo:7")
            .Build();

        await _mongoContainer.StartAsync(cancellationToken);
        MongoConnectionString = _mongoContainer.GetConnectionString();

        // Start Nightscout connected to MongoDB
        _nightscoutContainer = new ContainerBuilder()
            .WithImage(NightscoutImage)
            .WithPortBinding(NightscoutPort, true)
            .WithEnvironment("MONGODB_URI", MongoConnectionString)
            .WithEnvironment("MONGO_CONNECTION", MongoConnectionString)
            .WithEnvironment("API_SECRET", ApiSecret)
            .WithEnvironment("DISPLAY_UNITS", "mg/dl")
            .WithEnvironment("ENABLE", "careportal basal iob cob bwp cage sage iage bage pump openaps loop")
            .WithEnvironment("AUTH_DEFAULT_ROLES", "readable")
            .WithEnvironment("INSECURE_USE_HTTP", "true")
            .WithEnvironment("SECURE_HSTS_HEADER", "false")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPath("/api/v1/status.json")
                    .ForPort(NightscoutPort)))
            .Build();

        await _nightscoutContainer.StartAsync(cancellationToken);

        var host = _nightscoutContainer.Hostname;
        var port = _nightscoutContainer.GetMappedPublicPort(NightscoutPort);
        BaseUrl = $"http://{host}:{port}";

        // Create HttpClient with API secret header
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Add("api-secret", ApiSecretHash);
    }

    /// <summary>
    /// Clears all data from Nightscout collections
    /// </summary>
    public async Task CleanupDataAsync(CancellationToken cancellationToken = default)
    {
        if (_httpClient == null) return;

        var collections = new[] { "entries", "treatments", "devicestatus", "food", "profile" };

        foreach (var collection in collections)
        {
            try
            {
                // Nightscout bulk delete with find query
                await _httpClient.DeleteAsync(
                    $"/api/v1/{collection}?find[created_at][$exists]=true",
                    cancellationToken);
            }
            catch
            {
                // Ignore cleanup errors - collection might be empty
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient?.Dispose();

        if (_nightscoutContainer != null)
        {
            await _nightscoutContainer.StopAsync();
            await _nightscoutContainer.DisposeAsync();
        }

        if (_mongoContainer != null)
        {
            await _mongoContainer.StopAsync();
            await _mongoContainer.DisposeAsync();
        }
    }

    private static string ComputeSha1Hash(string input)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
