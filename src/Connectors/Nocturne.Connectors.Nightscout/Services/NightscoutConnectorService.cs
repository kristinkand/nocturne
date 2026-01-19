using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Nocturne.Connectors.Configurations;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.Core.Services;
using Nocturne.Core.Constants;
using Nocturne.Core.Models;

namespace Nocturne.Connectors.Nightscout.Services
{
    /// <summary>
    /// Connector service for Nightscout-to-Nightscout data synchronization
    /// Fetches data from one Nightscout instance and uploads to another
    /// </summary>
    public class NightscoutConnectorService : BaseConnectorService<NightscoutConnectorConfiguration>
    {
        private readonly NightscoutConnectorConfiguration _config;
        private readonly IRetryDelayStrategy _retryDelayStrategy;
        private readonly IRateLimitingStrategy _rateLimitingStrategy;

        public override string ConnectorSource => DataSources.NightscoutConnector;
        public override string ServiceName => "Nightscout";
        public override List<SyncDataType> SupportedDataTypes =>
            [
                SyncDataType.Glucose,
                SyncDataType.Treatments,
                SyncDataType.Profiles,
                SyncDataType.DeviceStatus,
                SyncDataType.Activity,
                SyncDataType.Food,
            ];

        public NightscoutConnectorService(
            HttpClient httpClient,
            IOptions<NightscoutConnectorConfiguration> config,
            ILogger<NightscoutConnectorService> logger,
            IRetryDelayStrategy retryDelayStrategy,
            IRateLimitingStrategy rateLimitingStrategy,
            IApiDataSubmitter? apiDataSubmitter = null,
            IConnectorMetricsTracker? metricsTracker = null,
            IConnectorStateService? stateService = null
        )
            : base(httpClient, logger, apiDataSubmitter, metricsTracker, stateService)
        {
            _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
            _retryDelayStrategy =
                retryDelayStrategy ?? throw new ArgumentNullException(nameof(retryDelayStrategy));
            _rateLimitingStrategy =
                rateLimitingStrategy
                ?? throw new ArgumentNullException(nameof(rateLimitingStrategy));
        }

        // Note: Authentication is now handled by NightscoutAuthHandler (DelegatingHandler)
        // JWT token management and API-secret header injection are centralized there

        /// <summary>
        /// Checks if the source Nightscout supports v3 API
        /// </summary>
        private async Task<bool> SupportsV3ApiAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/v3/version");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
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
                        "Testing connection to source Nightscout: {SourceEndpoint} (attempt {Attempt}/{MaxRetries})",
                        _config.SourceEndpoint,
                        attempt + 1,
                        maxRetries
                    );

                    // Test connection by fetching server status
                    var response = await _httpClient.GetAsync("/api/v1/status");

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();

                        // Check for retryable errors
                        if (
                            response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                            || response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
                            || response.StatusCode == System.Net.HttpStatusCode.InternalServerError
                            || response.StatusCode == System.Net.HttpStatusCode.BadGateway
                            || response.StatusCode == System.Net.HttpStatusCode.GatewayTimeout
                        )
                        {
                            lastException = new HttpRequestException(
                                $"HTTP {(int)response.StatusCode} {response.StatusCode}: {errorContent}"
                            );
                            _logger.LogWarning(
                                "Nightscout connection test failed with retryable error on attempt {Attempt}: {StatusCode} - {Error}",
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
                                "Failed to connect to source Nightscout with non-retryable error: {StatusCode} - {Error}",
                                response.StatusCode,
                                errorContent
                            );
                            _failedRequestCount++;
                            return false;
                        }
                    }
                    else
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var status = JsonSerializer.Deserialize<StatusResponse>(content);

                        if (status?.Status != "ok")
                        {
                            _logger.LogError(
                                "Source Nightscout status is not OK: {Status}",
                                status?.Status
                            );
                            _failedRequestCount++;
                            return false;
                        }

                        // Reset failed request count on successful connection
                        _failedRequestCount = 0;

                        _logger.LogInformation(
                            "Successfully connected to source Nightscout. Version: {Version}",
                            status.Version
                        );
                        return true;
                    }
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                    _logger.LogWarning(
                        ex,
                        "HTTP error during Nightscout connection test attempt {Attempt}: {Message}",
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
                        "Unexpected error during Nightscout connection test attempt {Attempt}",
                        attempt + 1
                    );
                    _failedRequestCount++;
                    return false;
                }
            }

            // All attempts failed
            _failedRequestCount++;
            _logger.LogError(
                "Nightscout connection test failed after {MaxRetries} attempts",
                maxRetries
            );

            if (lastException != null)
            {
                throw lastException;
            }

            return false;
        }

        #region V3 API Methods

        /// <summary>
        /// Gets the last modified timestamps for all collections using v3 API
        /// This is useful for efficient incremental syncing
        /// </summary>
        public async Task<Dictionary<string, long>?> GetLastModifiedAsync()
        {
            try
            {
                // Auth headers are automatically added by NightscoutAuthHandler
                var response = await _httpClient.GetAsync("/api/v3/lastModified");
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning(
                        "Failed to get lastModified from v3 API: {StatusCode} - {Error}",
                        response.StatusCode,
                        errorContent
                    );
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var json = JsonSerializer.Deserialize<JsonElement>(content);

                if (
                    json.TryGetProperty("result", out var result)
                    && result.TryGetProperty("collections", out var collections)
                )
                {
                    var lastModified = new Dictionary<string, long>();
                    foreach (var prop in collections.EnumerateObject())
                    {
                        if (prop.Value.TryGetInt64(out var timestamp))
                        {
                            lastModified[prop.Name] = timestamp;
                        }
                    }
                    return lastModified;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting lastModified from v3 API");
                return null;
            }
        }

        /// <summary>
        /// Generic v3 API fetch method for any collection with pagination support.
        /// Fetches all records within the specified date range using skip/limit pagination.
        /// </summary>
        private async Task<T[]> FetchCollectionV3Async<T>(
            string collection,
            DateTime? since = null,
            int limit = 1000,
            string? sortField = null,
            bool descending = true,
            DateTime? until = null
        )
        {
            var allItems = new List<T>();
            var skip = 0;
            var hasMore = true;
            var batchNumber = 0;

            while (hasMore)
            {
                try
                {
                    batchNumber++;
                    var urlBuilder = new StringBuilder(
                        $"/api/v3/{collection}?limit={limit}&skip={skip}"
                    );

                    if (!string.IsNullOrEmpty(sortField))
                    {
                        urlBuilder.Append(
                            descending ? $"&sort$desc={sortField}" : $"&sort={sortField}"
                        );
                    }

                    if (since.HasValue)
                    {
                        var sinceMs = ((DateTimeOffset)since.Value).ToUnixTimeMilliseconds();
                        // Use date field for filtering to match user expectations
                        urlBuilder.Append($"&{sortField ?? "date"}$gte={sinceMs}");
                    }

                    if (until.HasValue)
                    {
                        var untilMs = ((DateTimeOffset)until.Value).ToUnixTimeMilliseconds();
                        urlBuilder.Append($"&{sortField ?? "date"}$lte={untilMs}");
                    }

                    _logger.LogDebug(
                        "Fetching {Collection} from v3 API (batch {BatchNumber}): {Url}",
                        collection,
                        batchNumber,
                        urlBuilder.ToString()
                    );

                    // Auth headers are automatically added by NightscoutAuthHandler
                    var response = await _httpClient.GetAsync(urlBuilder.ToString());

                    if (
                        response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                        || response.StatusCode == System.Net.HttpStatusCode.NotFound
                    )
                    {
                        // Handler already tried to refresh JWT (for 401); or endpoint doesn't exist (404);
                        // fall back to v1 API
                        _logger.LogWarning(
                            "v3 API returned {StatusCode} for {Collection}, falling back to v1 API",
                            response.StatusCode,
                            collection
                        );
                        return await FetchCollectionV1Async<T>(
                            collection,
                            since,
                            limit,
                            sortField ?? "date",
                            until
                        );
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError(
                            "Failed to fetch {Collection} from v3 API: {StatusCode} - {Error}",
                            collection,
                            response.StatusCode,
                            errorContent
                        );
                        break;
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    var json = JsonSerializer.Deserialize<JsonElement>(content);

                    T[]? items = null;
                    if (json.TryGetProperty("result", out var result))
                    {
                        items = JsonSerializer.Deserialize<T[]>(result.GetRawText());
                    }

                    if (items == null || items.Length == 0)
                    {
                        hasMore = false;
                    }
                    else
                    {
                        allItems.AddRange(items);
                        skip += items.Length;

                        // If we got fewer items than the limit, we've reached the end
                        hasMore = items.Length >= limit;

                        _logger.LogDebug(
                            "Fetched {Count} {Collection} items from v3 API (batch {BatchNumber}, total: {Total})",
                            items.Length,
                            collection,
                            batchNumber,
                            allItems.Count
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error fetching {Collection} from v3 API (batch {BatchNumber})",
                        collection,
                        batchNumber
                    );
                    break;
                }
            }

            _logger.LogInformation(
                "Successfully fetched {Count} {Collection} items from v3 API in {BatchCount} batches",
                allItems.Count,
                collection,
                batchNumber
            );

            return allItems.ToArray();
        }

        /// <summary>
        /// Fallback v1 API fetch method for collections when v3/JWT is unavailable.
        /// Implements date-based cursor pagination to fetch all records in batches.
        /// </summary>
        private async Task<T[]> FetchCollectionV1Async<T>(
            string collection,
            DateTime? since = null,
            int limit = 1000,
            string dateField = "date",
            DateTime? until = null
        )
        {
            var allItems = new List<T>();
            var hasMore = true;
            var batchNumber = 0;

            // For cursor-based pagination, we track the oldest date seen
            // and use it as the upper bound for the next batch
            long? cursorDateMs = until.HasValue
                ? ((DateTimeOffset)until.Value).ToUnixTimeMilliseconds()
                : null;

            var sinceMs = since.HasValue
                ? ((DateTimeOffset)since.Value).ToUnixTimeMilliseconds()
                : (long?)null;

            while (hasMore)
            {
                try
                {
                    batchNumber++;

                    // V1 endpoints usually follow /api/v1/{collection}.json
                    var endpoint = collection == "entries" ? "entries.json" : collection;
                    var urlBuilder = new StringBuilder($"/api/v1/{endpoint}?count={limit}");

                    // Add lower bound (from date)
                    if (sinceMs.HasValue)
                    {
                        urlBuilder.Append($"&find[{dateField}][$gte]={sinceMs}");
                    }

                    // Add upper bound cursor (for pagination - fetch older records each batch)
                    if (cursorDateMs.HasValue)
                    {
                        urlBuilder.Append($"&find[{dateField}][$lt]={cursorDateMs}");
                    }

                    _logger.LogDebug(
                        "Fetching {Collection} from v1 API (batch {BatchNumber}): {Url}",
                        collection,
                        batchNumber,
                        urlBuilder.ToString()
                    );

                    // Auth headers are automatically added by NightscoutAuthHandler
                    var response = await _httpClient.GetAsync(urlBuilder.ToString());
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogWarning(
                            "V1 fallback fetch failed for {Collection}: {StatusCode} - {Error}",
                            collection,
                            response.StatusCode,
                            errorContent
                        );
                        break;
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    var items = JsonSerializer.Deserialize<T[]>(content) ?? Array.Empty<T>();

                    if (items.Length == 0)
                    {
                        hasMore = false;
                    }
                    else
                    {
                        allItems.AddRange(items);

                        // Find the oldest date in this batch to use as cursor for next batch
                        var oldestDateMs = GetOldestDateFromBatch(items, dateField);
                        if (oldestDateMs.HasValue)
                        {
                            // Check if we've reached or passed our lower bound
                            if (sinceMs.HasValue && oldestDateMs.Value <= sinceMs.Value)
                            {
                                hasMore = false;
                            }
                            else
                            {
                                cursorDateMs = oldestDateMs;
                            }
                        }
                        else
                        {
                            // Couldn't extract date, stop to avoid infinite loop
                            hasMore = false;
                        }

                        // If we got fewer items than limit, we've reached the end
                        if (items.Length < limit)
                        {
                            hasMore = false;
                        }

                        _logger.LogDebug(
                            "Fetched {Count} {Collection} items from v1 API (batch {BatchNumber}, total: {Total})",
                            items.Length,
                            collection,
                            batchNumber,
                            allItems.Count
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error in v1 fallback fetch for {Collection} (batch {BatchNumber})",
                        collection,
                        batchNumber
                    );
                    break;
                }
            }

            _logger.LogInformation(
                "Successfully fetched {Count} {Collection} items from v1 API in {BatchCount} batches",
                allItems.Count,
                collection,
                batchNumber
            );

            return allItems.ToArray();
        }

        /// <summary>
        /// Extracts the oldest (minimum) date from a batch of items using reflection.
        /// Returns the date as Unix milliseconds for cursor pagination.
        /// </summary>
        private long? GetOldestDateFromBatch<T>(T[] items, string dateField)
        {
            if (items.Length == 0)
                return null;

            long? oldestMs = null;

            foreach (var item in items)
            {
                if (item == null)
                    continue;

                var dateValue = GetDateFieldValue(item, dateField);
                if (dateValue.HasValue)
                {
                    var ms = ((DateTimeOffset)dateValue.Value).ToUnixTimeMilliseconds();
                    if (!oldestMs.HasValue || ms < oldestMs.Value)
                    {
                        oldestMs = ms;
                    }
                }
            }

            return oldestMs;
        }

        /// <summary>
        /// Gets the date field value from an item, handling various field names and types.
        /// </summary>
        private DateTime? GetDateFieldValue<T>(T item, string dateField)
        {
            if (item == null)
                return null;

            var type = typeof(T);

            // Try the specified date field first (case-insensitive)
            var property = type.GetProperties()
                .FirstOrDefault(p =>
                    string.Equals(p.Name, dateField, StringComparison.OrdinalIgnoreCase)
                );

            // Fallback to common date field names
            property ??= type.GetProperties()
                .FirstOrDefault(p =>
                    string.Equals(p.Name, "Date", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.Name, "Mills", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.Name, "CreatedAt", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.Name, "Created_At", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.Name, "Timestamp", StringComparison.OrdinalIgnoreCase)
                );

            if (property == null)
                return null;

            var value = property.GetValue(item);

            return value switch
            {
                DateTime dt => dt,
                DateTimeOffset dto => dto.DateTime,
                long ms => DateTimeOffset.FromUnixTimeMilliseconds(ms).DateTime,
                string s when DateTime.TryParse(s, out var parsed) => parsed,
                _ => null,
            };
        }

        #endregion

        public override async Task<IEnumerable<Entry>> FetchGlucoseDataAsync(DateTime? since = null)
        {
            return await FetchCollectionV3Async<Entry>(
                "entries",
                since,
                1000,
                "date",
                true,
                until: null
            );
        }

        protected override async Task<IEnumerable<Entry>> FetchGlucoseDataRangeAsync(
            DateTime? from,
            DateTime? to
        )
        {
            return await FetchCollectionV3Async<Entry>(
                "entries",
                from,
                1000,
                "date",
                true,
                until: to
            );
        }

        protected override async Task<IEnumerable<Treatment>> FetchTreatmentsAsync(
            DateTime? from,
            DateTime? to
        )
        {
            return await FetchCollectionV3Async<Treatment>(
                "treatments",
                from,
                1000,
                "created_at",
                true,
                until: to
            );
        }

        protected override async Task<IEnumerable<DeviceStatus>> FetchDeviceStatusAsync(
            DateTime? from,
            DateTime? to
        )
        {
            return await FetchCollectionV3Async<DeviceStatus>(
                "devicestatus",
                from,
                1000,
                "created_at",
                true,
                until: to
            );
        }

        protected override async Task<IEnumerable<Profile>> FetchProfilesAsync(
            DateTime? from,
            DateTime? to
        )
        {
            return await FetchCollectionV3Async<Profile>(
                "profiles",
                from,
                100,
                "startDate",
                true,
                until: to
            );
        }

        protected override async Task<IEnumerable<Activity>> FetchActivitiesAsync(
            DateTime? from,
            DateTime? to
        )
        {
            return await FetchCollectionV3Async<Activity>(
                "activity",
                from,
                1000,
                "created_at",
                true,
                until: to
            );
        }

        protected override async Task<IEnumerable<Food>> FetchFoodsAsync(
            DateTime? from,
            DateTime? to
        )
        {
            return await FetchCollectionV3Async<Food>(
                "food",
                from,
                1000,
                "created_at",
                true,
                until: to
            );
        }

        private IEnumerable<Entry> TransformBatchDataToEntries(Entry[] batchData)
        {
            if (batchData == null || batchData.Length == 0)
            {
                return Enumerable.Empty<Entry>();
            }

            return batchData
                .Where(entry => entry != null && (entry.Mgdl > 0 || entry.Sgv > 0))
                .OrderByDescending(entry => entry.Date)
                .ToList();
        }
    }
}
