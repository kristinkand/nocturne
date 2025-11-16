using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Nocturne.Connectors.Core.Constants;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.Core.Services;
using Nocturne.Connectors.Dexcom.Constants;
using Nocturne.Connectors.Dexcom.Models;
using Nocturne.Core.Models;

#nullable enable

namespace Nocturne.Connectors.Dexcom.Services
{
    /// <summary>
    /// Connector service for Dexcom Share data source
    /// Enhanced implementation based on the original nightscout-connect Dexcom Share implementation
    /// </summary>
    public class DexcomConnectorService : BaseConnectorService<DexcomConnectorConfiguration>
    {
        private readonly DexcomConnectorConfiguration _config;
        private new readonly ILogger<DexcomConnectorService> _logger;
        private readonly IRetryDelayStrategy _retryDelayStrategy;
        private readonly IRateLimitingStrategy _rateLimitingStrategy;
        private string? _sessionId;
        private DateTime _sessionExpiresAt;
        private int _failedRequestCount = 0;
        private static readonly Dictionary<string, string> KnownServers = new()
        {
            { "us", DexcomConstants.Servers.US },
            { "ous", DexcomConstants.Servers.OUS },
        };
        private static readonly Dictionary<int, Direction> TrendDirections = new()
        {
            { 0, Direction.NONE },
            { 1, Direction.DoubleUp },
            { 2, Direction.SingleUp },
            { 3, Direction.FortyFiveUp },
            { 4, Direction.Flat },
            { 5, Direction.FortyFiveDown },
            { 6, Direction.SingleDown },
            { 7, Direction.DoubleDown },
            { 8, Direction.NotComputable },
            { 9, Direction.RateOutOfRange },
        };

        /// <summary>
        /// Gets whether the connector is in a healthy state based on recent request failures
        /// </summary>
        public bool IsHealthy => _failedRequestCount < 5;

        /// <summary>
        /// Gets the source identifier for this connector
        /// </summary>
        public override string ConnectorSource => "dexcom";

        /// <summary>
        /// Gets the number of consecutive failed requests
        /// </summary>
        public int FailedRequestCount => _failedRequestCount;

        /// <summary>
        /// Resets the failed request counter
        /// </summary>
        public void ResetFailedRequestCount()
        {
            _failedRequestCount = 0;
            _logger.LogInformation("Dexcom connector failed request count reset");
        }

        public override string ServiceName => "Dexcom Share";

        public DexcomConnectorService(
            DexcomConnectorConfiguration config,
            ILogger<DexcomConnectorService> logger,
            IApiDataSubmitter apiDataSubmitter
        )
            : base(apiDataSubmitter, logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _retryDelayStrategy = new ProductionRetryDelayStrategy();
            _rateLimitingStrategy = new ProductionRateLimitingStrategy(
                LoggerFactory
                    .Create(builder => builder.AddConsole())
                    .CreateLogger<ProductionRateLimitingStrategy>()
            );

            ConfigureHttpClient();
        }

        public DexcomConnectorService(
            DexcomConnectorConfiguration config,
            ILogger<DexcomConnectorService> logger
        )
            : base()
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _retryDelayStrategy = new ProductionRetryDelayStrategy();
            _rateLimitingStrategy = new ProductionRateLimitingStrategy(
                LoggerFactory
                    .Create(builder => builder.AddConsole())
                    .CreateLogger<ProductionRateLimitingStrategy>()
            );

            ConfigureHttpClient();
        }

        public DexcomConnectorService(
            DexcomConnectorConfiguration config,
            ILogger<DexcomConnectorService> logger,
            HttpClient httpClient
        )
            : base(httpClient)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _retryDelayStrategy = new ProductionRetryDelayStrategy();
            _rateLimitingStrategy = new ProductionRateLimitingStrategy(
                LoggerFactory
                    .Create(builder => builder.AddConsole())
                    .CreateLogger<ProductionRateLimitingStrategy>()
            );

            ConfigureHttpClient();
        }

        public DexcomConnectorService(
            DexcomConnectorConfiguration config,
            ILogger<DexcomConnectorService> logger,
            HttpClient httpClient,
            IRetryDelayStrategy retryDelayStrategy,
            IRateLimitingStrategy rateLimitingStrategy
        )
            : base(httpClient)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _retryDelayStrategy =
                retryDelayStrategy ?? throw new ArgumentNullException(nameof(retryDelayStrategy));
            _rateLimitingStrategy =
                rateLimitingStrategy
                ?? throw new ArgumentNullException(nameof(rateLimitingStrategy));

            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            var server = !string.IsNullOrEmpty(_config.DexcomServer)
                ? _config.DexcomServer
                : KnownServers.GetValueOrDefault(
                    _config.DexcomRegion.ToLowerInvariant(),
                    KnownServers["us"]
                );

            _httpClient.BaseAddress = new Uri($"https://{server}");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Nocturne-Connect/1.0");
        }

        public override async Task<bool> AuthenticateAsync()
        {
            const int maxRetries = 3;
            HttpRequestException? lastException = null;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation(
                        "Authenticating with Dexcom Share for account: {Username} (attempt {Attempt}/{MaxRetries})",
                        _config.DexcomUsername,
                        attempt + 1,
                        maxRetries
                    );

                    var authPayload = new
                    {
                        password = _config.DexcomPassword,
                        applicationId = "d89443d2-327c-4a6f-89e5-496bbb0317db",
                        accountName = _config.DexcomUsername,
                    };

                    var json = JsonSerializer.Serialize(authPayload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(
                        "/ShareWebServices/Services/General/AuthenticatePublisherAccount",
                        content
                    );

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();

                        // Check for rate limiting or temporary errors
                        if (
                            response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                            || response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
                            || response.StatusCode == System.Net.HttpStatusCode.InternalServerError
                        )
                        {
                            lastException = new HttpRequestException(
                                $"HTTP {(int)response.StatusCode} {response.StatusCode}: {errorContent}"
                            );
                            _logger.LogWarning(
                                "Dexcom authentication failed with retryable error on attempt {Attempt}: {StatusCode} - {Error}",
                                attempt + 1,
                                response.StatusCode,
                                errorContent
                            );

                            if (attempt < maxRetries - 1)
                            {
                                _logger.LogInformation(
                                    "Applying retry backoff before attempt {NextAttempt}",
                                    attempt + 2
                                );
                                await _retryDelayStrategy.ApplyRetryDelayAsync(attempt);
                                continue;
                            }
                        }
                        else
                        {
                            // Non-retryable error (e.g., invalid credentials)
                            _logger.LogError(
                                "Dexcom authentication failed with non-retryable error: {StatusCode} - {Error}",
                                response.StatusCode,
                                errorContent
                            );
                            _failedRequestCount++;
                            return false;
                        }
                    }
                    else
                    {
                        var accountId = await response.Content.ReadAsStringAsync();
                        accountId = accountId.Trim('"'); // Remove quotes from JSON string

                        if (string.IsNullOrEmpty(accountId))
                        {
                            _logger.LogError("Dexcom authentication returned empty account ID");
                            _failedRequestCount++;
                            return false;
                        }

                        // Now get session ID
                        var sessionPayload = new
                        {
                            password = _config.DexcomPassword,
                            applicationId = "d89443d2-327c-4a6f-89e5-496bbb0317db",
                            accountId = accountId,
                        };

                        json = JsonSerializer.Serialize(sessionPayload);
                        content = new StringContent(json, Encoding.UTF8, "application/json");

                        response = await _httpClient.PostAsync(
                            "/ShareWebServices/Services/General/LoginPublisherAccountById",
                            content
                        );

                        if (!response.IsSuccessStatusCode)
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();

                            // Check for retryable errors in session creation
                            if (
                                response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                                || response.StatusCode
                                    == System.Net.HttpStatusCode.ServiceUnavailable
                                || response.StatusCode
                                    == System.Net.HttpStatusCode.InternalServerError
                            )
                            {
                                lastException = new HttpRequestException(
                                    $"HTTP {(int)response.StatusCode} {response.StatusCode}: {errorContent}"
                                );
                                _logger.LogWarning(
                                    "Dexcom session creation failed with retryable error on attempt {Attempt}: {StatusCode} - {Error}",
                                    attempt + 1,
                                    response.StatusCode,
                                    errorContent
                                );

                                if (attempt < maxRetries - 1)
                                {
                                    _logger.LogInformation(
                                        "Applying retry backoff before attempt {NextAttempt}",
                                        attempt + 2
                                    );
                                    await _retryDelayStrategy.ApplyRetryDelayAsync(attempt);
                                    continue;
                                }
                            }
                            else
                            {
                                _logger.LogError(
                                    "Dexcom session creation failed with non-retryable error: {StatusCode} - {Error}",
                                    response.StatusCode,
                                    errorContent
                                );
                                _failedRequestCount++;
                                return false;
                            }
                        }
                        else
                        {
                            _sessionId = await response.Content.ReadAsStringAsync();
                            _sessionId = _sessionId.Trim('"'); // Remove quotes from JSON string

                            if (string.IsNullOrEmpty(_sessionId))
                            {
                                _logger.LogError(
                                    "Dexcom session creation returned empty session ID"
                                );
                                _failedRequestCount++;
                                return false;
                            }

                            // Set session expiration (Dexcom sessions typically last 24 hours)
                            _sessionExpiresAt = DateTime.UtcNow.AddHours(23); // Add buffer

                            // Reset failed request count on successful authentication
                            _failedRequestCount = 0;

                            _logger.LogInformation(
                                "Dexcom Share authentication successful, session expires at {ExpiresAt}",
                                _sessionExpiresAt
                            );
                            return true;
                        }
                    }
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                    _logger.LogWarning(
                        ex,
                        "HTTP error during Dexcom authentication attempt {Attempt}: {Message}",
                        attempt + 1,
                        ex.Message
                    );

                    if (attempt < maxRetries - 1)
                    {
                        _logger.LogInformation(
                            "Applying retry backoff before attempt {NextAttempt}",
                            attempt + 2
                        );
                        await _retryDelayStrategy.ApplyRetryDelayAsync(attempt);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Unexpected error during Dexcom authentication attempt {Attempt}",
                        attempt + 1
                    );
                    _failedRequestCount++;
                    return false;
                }
            }

            // All attempts failed
            _failedRequestCount++;
            _logger.LogError(
                "Dexcom authentication failed after {MaxRetries} attempts",
                maxRetries
            );

            if (lastException != null)
            {
                throw lastException;
            }

            return false;
        }

        private bool IsSessionExpired()
        {
            return string.IsNullOrEmpty(_sessionId) || DateTime.UtcNow >= _sessionExpiresAt;
        }

        public override async Task<IEnumerable<Entry>> FetchGlucoseDataAsync(DateTime? since = null)
        {
            try
            {
                // Check if session is expired and re-authenticate if needed
                if (IsSessionExpired())
                {
                    _logger.LogInformation("Session expired, attempting to re-authenticate");
                    if (!await AuthenticateAsync())
                    {
                        _failedRequestCount++;
                        return Enumerable.Empty<Entry>();
                    }
                } // Apply rate limiting
                await _rateLimitingStrategy.ApplyDelayAsync(0);

                return await FetchGlucoseDataWithRetryAsync(since);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching glucose data from Dexcom Share");
                _failedRequestCount++;
                return Enumerable.Empty<Entry>();
            }
        }

        private async Task<IEnumerable<Entry>> FetchGlucoseDataWithRetryAsync(
            DateTime? since = null
        )
        {
            const int maxRetries = 3;
            HttpRequestException? lastException = null;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    _logger.LogDebug(
                        "Fetching Dexcom glucose data (attempt {Attempt}/{MaxRetries})",
                        attempt + 1,
                        maxRetries
                    );

                    // Calculate time range
                    var twoDaysAgo = DateTime.UtcNow.AddDays(-2);
                    var startTime = since.HasValue
                        ? (since.Value > twoDaysAgo ? since.Value : twoDaysAgo)
                        : twoDaysAgo;

                    var timeDiff = DateTime.UtcNow - startTime;
                    var maxCount = Math.Ceiling(timeDiff.TotalMinutes / 5); // 5-minute intervals
                    var minutes = (int)(maxCount * 5);

                    var url =
                        $"/ShareWebServices/Services/Publisher/ReadPublisherLatestGlucoseValues?sessionID={_sessionId}&minutes={minutes}&maxCount={(int)maxCount}";

                    var response = await _httpClient.PostAsync(
                        url,
                        new StringContent("{}", Encoding.UTF8, "application/json")
                    );

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();

                        // Handle session expiration
                        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            _logger.LogWarning(
                                "Dexcom session expired, attempting re-authentication"
                            );
                            _sessionId = null;
                            _sessionExpiresAt = DateTime.MinValue;

                            if (await AuthenticateAsync())
                            {
                                // Retry with new session on same attempt
                                continue;
                            }
                            else
                            {
                                _logger.LogError("Failed to re-authenticate with Dexcom Share");
                                _failedRequestCount++;
                                return Enumerable.Empty<Entry>();
                            }
                        }

                        // Check for retryable errors
                        if (
                            response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                            || response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
                            || response.StatusCode == System.Net.HttpStatusCode.InternalServerError
                        )
                        {
                            lastException = new HttpRequestException(
                                $"HTTP {(int)response.StatusCode} {response.StatusCode}: {errorContent}"
                            );
                            _logger.LogWarning(
                                "Dexcom data fetch failed with retryable error on attempt {Attempt}: {StatusCode} - {Error}",
                                attempt + 1,
                                response.StatusCode,
                                errorContent
                            );

                            if (attempt < maxRetries - 1)
                            {
                                _logger.LogInformation(
                                    "Applying retry backoff before attempt {NextAttempt}",
                                    attempt + 2
                                );
                                await _retryDelayStrategy.ApplyRetryDelayAsync(attempt);
                                continue;
                            }
                        }
                        else
                        {
                            // Non-retryable error
                            _logger.LogError(
                                "Dexcom data fetch failed with non-retryable error: {StatusCode} - {Error}",
                                response.StatusCode,
                                errorContent
                            );
                            _failedRequestCount++;
                            return Enumerable.Empty<Entry>();
                        }
                    }
                    else
                    {
                        var jsonContent = await response.Content.ReadAsStringAsync();
                        var dexcomEntries = JsonSerializer.Deserialize<DexcomEntry[]>(jsonContent);

                        if (dexcomEntries == null || dexcomEntries.Length == 0)
                        {
                            _logger.LogDebug("No glucose data returned from Dexcom Share");
                            return Enumerable.Empty<Entry>();
                        }

                        var glucoseEntries = dexcomEntries
                            .Where(entry => entry != null && entry.Value > 0)
                            .Select(ConvertDexcomEntry)
                            .Where(entry =>
                                entry != null && (!since.HasValue || entry.Date > since.Value)
                            )
                            .OrderBy(entry => entry.Date)
                            .ToList();

                        // Reset failed request count on successful fetch
                        _failedRequestCount = 0;

                        _logger.LogInformation(
                            "Successfully fetched {Count} glucose entries from Dexcom Share",
                            glucoseEntries.Count
                        );
                        return glucoseEntries;
                    }
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                    _logger.LogWarning(
                        ex,
                        "HTTP error during Dexcom data fetch attempt {Attempt}: {Message}",
                        attempt + 1,
                        ex.Message
                    );

                    if (attempt < maxRetries - 1)
                    {
                        _logger.LogInformation(
                            "Applying retry backoff before attempt {NextAttempt}",
                            attempt + 2
                        );
                        await _retryDelayStrategy.ApplyRetryDelayAsync(attempt);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(
                        ex,
                        "JSON parsing error during Dexcom data fetch attempt {Attempt}",
                        attempt + 1
                    );
                    _failedRequestCount++;
                    return Enumerable.Empty<Entry>();
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Unexpected error during Dexcom data fetch attempt {Attempt}",
                        attempt + 1
                    );
                    _failedRequestCount++;
                    return Enumerable.Empty<Entry>();
                }
            }

            // All attempts failed
            _failedRequestCount++;
            _logger.LogError("Dexcom data fetch failed after {MaxRetries} attempts", maxRetries);

            if (lastException != null)
            {
                throw lastException;
            }

            return Enumerable.Empty<Entry>();
        }

        /// <summary>
        /// Syncs Dexcom data using message publishing when available, with fallback to direct API
        /// </summary>
        /// <param name="config">Connector configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if sync was successful</returns>
        public async Task<bool> SyncDexcomDataAsync(
            DexcomConnectorConfiguration config,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                _logger.LogInformation(
                    "Starting Dexcom data sync using {Mode} mode",
                    config.UseAsyncProcessing ? "asynchronous" : "direct API"
                );

                // Use the hybrid sync method from BaseConnectorService
                var success = await SyncDataAsync(config, cancellationToken);

                if (success)
                {
                    _logger.LogInformation("Dexcom data sync completed successfully");
                }
                else
                {
                    _logger.LogWarning("Dexcom data sync failed");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Dexcom data sync");
                return false;
            }
        }

        private Entry ConvertDexcomEntry(DexcomEntry dexcomEntry)
        {
            try
            {
                // Parse Dexcom's date format: /Date(1426292016000-0700)/
                var wallTimeMatch = System.Text.RegularExpressions.Regex.Match(
                    dexcomEntry.WT,
                    @"\((\d+)\)"
                );
                if (!wallTimeMatch.Success)
                {
                    _logger.LogWarning(
                        "Could not parse Dexcom timestamp: {Timestamp}",
                        dexcomEntry.WT
                    );
                    return new Entry { Type = "sgv", Device = "nightscout-connect-dexcom-share" };
                }

                var wallTimeMillis = long.Parse(wallTimeMatch.Groups[1].Value);
                var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(wallTimeMillis).DateTime;

                var direction = TrendDirections.GetValueOrDefault(
                    dexcomEntry.Trend,
                    Direction.NotComputable
                );
                return new Entry
                {
                    Date = timestamp,
                    Sgv = dexcomEntry.Value,
                    Direction = direction.ToString(),
                    Device = "nightscout-connect-dexcom-share",
                    Type = "sgv",
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error converting Dexcom entry: {@Entry}", dexcomEntry);
                return new Entry { Type = "sgv", Device = "nightscout-connect-dexcom-share" };
            }
        }

        private class DexcomEntry
        {
            public string DT { get; set; } = string.Empty;
            public string ST { get; set; } = string.Empty;
            public int Trend { get; set; }
            public int Value { get; set; }
            public string WT { get; set; } = string.Empty;
        }
    }
}
