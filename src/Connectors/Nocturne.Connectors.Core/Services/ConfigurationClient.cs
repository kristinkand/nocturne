using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Core.Interfaces;

namespace Nocturne.Connectors.Core.Services;

/// <summary>
/// Client for connectors to fetch their configuration from the API.
/// Supports both HTTP fetching and SignalR subscriptions for real-time updates.
/// </summary>
public class ConfigurationClient : IConfigurationClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ConfigurationClient> _logger;
    private readonly string _baseUrl;
    private HubConnection? _hubConnection;
    private bool _disposed;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ConfigurationClient(
        HttpClient httpClient,
        string baseUrl,
        ILogger<ConfigurationClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    /// <inheritdoc />
    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    /// <inheritdoc />
    public async Task<TConfig?> GetConfigurationAsync<TConfig>(string connectorName, CancellationToken ct = default)
        where TConfig : class, new()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/internal/config/{connectorName}", ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogDebug("No configuration found for connector {ConnectorName}", connectorName);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var configResponse = await response.Content.ReadFromJsonAsync<ConfigurationResponse>(
                _jsonOptions, ct);

            if (configResponse?.Configuration == null)
            {
                return null;
            }

            // Deserialize the configuration JSON to the typed config
            return JsonSerializer.Deserialize<TConfig>(
                configResponse.Configuration.RootElement.GetRawText(), _jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get configuration for connector {ConnectorName}", connectorName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<JsonDocument?> GetConfigurationJsonAsync(string connectorName, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/internal/config/{connectorName}", ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            var configResponse = await response.Content.ReadFromJsonAsync<ConfigurationResponse>(
                _jsonOptions, ct);

            return configResponse?.Configuration;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get configuration JSON for connector {ConnectorName}", connectorName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<JsonDocument> GetSchemaAsync(string connectorName, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/internal/config/{connectorName}/schema", ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonDocument.Parse(json);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get schema for connector {ConnectorName}", connectorName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(string connectorName, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/internal/config/{connectorName}", ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }

            response.EnsureSuccessStatusCode();

            var configResponse = await response.Content.ReadFromJsonAsync<ConfigurationResponse>(
                _jsonOptions, ct);

            return configResponse?.IsActive ?? false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to check enabled status for connector {ConnectorName}", connectorName);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task SubscribeToChangesAsync(string connectorName, CancellationToken ct = default)
    {
        await EnsureHubConnectionAsync(ct);

        if (_hubConnection != null)
        {
            await _hubConnection.InvokeAsync("Subscribe", connectorName, ct);
            _logger.LogDebug("Subscribed to configuration changes for {ConnectorName}", connectorName);
        }
    }

    /// <inheritdoc />
    public async Task UnsubscribeFromChangesAsync(string connectorName, CancellationToken ct = default)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("Unsubscribe", connectorName, ct);
            _logger.LogDebug("Unsubscribed from configuration changes for {ConnectorName}", connectorName);
        }
    }

    private async Task EnsureHubConnectionAsync(CancellationToken ct)
    {
        if (_hubConnection == null)
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{_baseUrl}/hubs/config", options =>
                {
                    // Copy authentication headers from the HTTP client
                    // In mTLS mode, the certificate will be used instead
                })
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<ConfigChangeEvent>("configChanged", OnConfigChanged);
            _hubConnection.Reconnected += OnReconnected;
            _hubConnection.Closed += OnClosed;
        }

        if (_hubConnection.State == HubConnectionState.Disconnected)
        {
            try
            {
                await _hubConnection.StartAsync(ct);
                _logger.LogInformation("Connected to ConfigHub at {BaseUrl}", _baseUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to ConfigHub at {BaseUrl}", _baseUrl);
                throw;
            }
        }
    }

    private void OnConfigChanged(ConfigChangeEvent change)
    {
        _logger.LogDebug("Received config change for {ConnectorName}: {ChangeType}",
            change.ConnectorName, change.ChangeType);

        ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs
        {
            ConnectorName = change.ConnectorName,
            ChangeType = change.ChangeType,
            Timestamp = change.Timestamp
        });
    }

    private Task OnReconnected(string? connectionId)
    {
        _logger.LogInformation("Reconnected to ConfigHub with connection ID {ConnectionId}", connectionId);
        return Task.CompletedTask;
    }

    private Task OnClosed(Exception? error)
    {
        if (error != null)
        {
            _logger.LogWarning(error, "ConfigHub connection closed with error");
        }
        else
        {
            _logger.LogInformation("ConfigHub connection closed");
        }
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Internal response model for configuration API.
    /// </summary>
    private class ConfigurationResponse
    {
        public string ConnectorName { get; set; } = string.Empty;
        public JsonDocument? Configuration { get; set; }
        public int SchemaVersion { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset LastModified { get; set; }
        public string? ModifiedBy { get; set; }
    }

    /// <summary>
    /// Internal event model for SignalR config changes.
    /// </summary>
    private class ConfigChangeEvent
    {
        public string ConnectorName { get; set; } = string.Empty;
        public string ChangeType { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
        public string? ModifiedBy { get; set; }
    }
}
