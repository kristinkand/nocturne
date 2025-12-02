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
        private readonly IConnectorFileService<Entry[]>? _fileService;
        private int _failedRequestCount = 0;

        /// <summary>
        /// Gets the connector source identifier
        /// </summary>
        public override string ConnectorSource => "nightscout";

        /// <summary>
        /// Gets whether the connector is in a healthy state based on recent request failures
        /// </summary>
        public bool IsHealthy => _failedRequestCount < 5;

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
            _logger.LogInformation("Nightscout connector failed request count reset");
        }

        public override string ServiceName => "Nightscout";

        public NightscoutConnectorService(
            HttpClient httpClient,
            IOptions<NightscoutConnectorConfiguration> config,
            ILogger<NightscoutConnectorService> logger,
            IRetryDelayStrategy retryDelayStrategy,
            IRateLimitingStrategy rateLimitingStrategy,
            IApiDataSubmitter? apiDataSubmitter = null
        )
            : base(httpClient, logger, apiDataSubmitter)
        {
            _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
            _retryDelayStrategy =
                retryDelayStrategy ?? throw new ArgumentNullException(nameof(retryDelayStrategy));
            _rateLimitingStrategy =
                rateLimitingStrategy
                ?? throw new ArgumentNullException(nameof(rateLimitingStrategy));
        }

        /// <summary>
        /// Hash API secret using SHA1 to match Nightscout's expected format
        /// </summary>
        private static string HashApiSecret(string apiSecret)
        {
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            var hashBytes = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(apiSecret));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
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

        public override async Task<IEnumerable<Entry>> FetchGlucoseDataAsync(DateTime? since = null)
        {
            // Use the base class helper for file I/O and data fetching
            var entries = await FetchWithOptionalFileIOAsync(
                _config,
                async (s) => await FetchBatchDataAsync(s),
                TransformBatchDataToEntries,
                _fileService,
                "nightscout_batch",
                since
            );

            _logger.LogInformation(
                "[{ConnectorSource}] Retrieved {Count} glucose entries from Nightscout",
                ConnectorSource,
                entries.Count()
            );

            return entries;
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

        private async Task<Entry[]?> FetchBatchDataAsync(DateTime? since = null)
        {
            try
            {
                // Apply rate limiting
                await _rateLimitingStrategy.ApplyDelayAsync(0);
                // Use a much higher count to get more data per request
                int hundredAndTwentyDays = 120 * 24 * 60 * 5;
                var result = await FetchRawDataWithRetryAsync(since, hundredAndTwentyDays);

                // Log batch data summary
                if (result != null && result.Length > 0)
                {
                    var validEntries = result
                        .Where(e => e != null && (e.Mgdl > 0 || e.Sgv > 0))
                        .ToArray();
                    var minDate =
                        validEntries.Length > 0 ? validEntries.Min(e => e.Date) : DateTime.MinValue;
                    var maxDate =
                        validEntries.Length > 0 ? validEntries.Max(e => e.Date) : DateTime.MinValue;

                    _logger.LogInformation(
                        "[{ConnectorSource}] Fetched Nightscout batch data: TotalEntries={TotalCount}, ValidEntries={ValidCount}, DateRange={MinDate:yyyy-MM-dd HH:mm} to {MaxDate:yyyy-MM-dd HH:mm}",
                        ConnectorSource,
                        result.Length,
                        validEntries.Length,
                        minDate,
                        maxDate
                    );
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[{ConnectorSource}] Error fetching glucose data from source Nightscout",
                    ConnectorSource
                );
                _failedRequestCount++;
                return null;
            }
        }

        private async Task<Entry[]?> FetchRawDataWithRetryAsync(
            DateTime? since = null,
            int count = 120 * 24 * 60 * 5 // Increase default limit to 100k to get more data per request
        )
        {
            const int maxRetries = 3;
            HttpRequestException? lastException = null;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    _logger.LogDebug(
                        "Fetching Nightscout glucose data (attempt {Attempt}/{MaxRetries})",
                        attempt + 1,
                        maxRetries
                    );

                    var startTime = since ?? DateTime.UtcNow.AddHours(-24); // Default to last 24 hours

                    var url =
                        $"/api/v1/entries/sgv.json?find[date][$gte]={ToUnixTimestamp(startTime)}&count={count}";

                    var response = await _httpClient.GetAsync(url);

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
                                "Nightscout data fetch failed with retryable error on attempt {Attempt}: {StatusCode} - {Error}",
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
                                "Nightscout data fetch failed with non-retryable error: {StatusCode} - {Error}",
                                response.StatusCode,
                                errorContent
                            );
                            _failedRequestCount++;
                            throw new HttpRequestException(
                                $"HTTP error {response.StatusCode}: {errorContent}"
                            );
                        }
                    }
                    else
                    {
                        var jsonContent = await response.Content.ReadAsStringAsync();
                        var entries = JsonSerializer.Deserialize<Entry[]>(jsonContent);

                        // Reset failed request count on successful fetch
                        _failedRequestCount = 0;

                        return entries ?? Array.Empty<Entry>();
                    }
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                    _logger.LogWarning(
                        ex,
                        "HTTP error during Nightscout data fetch attempt {Attempt}: {Message}",
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
                        "JSON parsing error during Nightscout data fetch attempt {Attempt}",
                        attempt + 1
                    );
                    _failedRequestCount++;
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Unexpected error during Nightscout data fetch attempt {Attempt}",
                        attempt + 1
                    );
                    _failedRequestCount++;
                    throw;
                }
            }

            // All attempts failed
            _failedRequestCount++;
            _logger.LogError(
                "Nightscout data fetch failed after {MaxRetries} attempts",
                maxRetries
            );

            if (lastException != null)
            {
                throw lastException;
            }
            return null;
        }

        /// <summary>
        /// Fetch treatments from source Nightscout
        /// </summary>
        public async Task<IEnumerable<Treatment>> FetchTreatmentsAsync(DateTime? since = null)
        {
            try
            {
                var startTime = since ?? DateTime.UtcNow.AddHours(-24);
                int count = 120 * 24 * 60 * 5;

                var url =
                    $"/api/v1/treatments.json?find[created_at][$gte]={startTime:yyyy-MM-ddTHH:mm:ss.fffZ}&count={count}";

                _logger.LogDebug("Fetching Nightscout treatments: {Url}", url);

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError(
                        "Failed to fetch Nightscout treatments: {StatusCode} - {Error}",
                        response.StatusCode,
                        errorContent
                    );
                    return Enumerable.Empty<Treatment>();
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var treatments = JsonSerializer.Deserialize<Treatment[]>(jsonContent);

                if (treatments == null || treatments.Length == 0)
                {
                    _logger.LogDebug("No treatments returned from source Nightscout");
                    return Enumerable.Empty<Treatment>();
                }

                var filteredTreatments = treatments
                    .Where(treatment =>
                        treatment != null
                        && (
                            !since.HasValue
                            || (
                                treatment.CreatedAt != null
                                && DateTime.Parse(treatment.CreatedAt) > since.Value
                            )
                        )
                    )
                    .OrderBy(treatment => DateTime.Parse(treatment.CreatedAt!))
                    .ToList();

                _logger.LogInformation(
                    "Successfully fetched {Count} treatments from source Nightscout",
                    filteredTreatments.Count
                );
                return filteredTreatments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching treatments from source Nightscout");
                return Enumerable.Empty<Treatment>();
            }
        }

        /// <summary>
        /// Fetch device status entries from source Nightscout
        /// </summary>
        public async Task<IEnumerable<DeviceStatus>> FetchDeviceStatusAsync(DateTime? since = null)
        {
            try
            {
                var startTime = since ?? DateTime.UtcNow.AddHours(-24);
                var count = 100;

                var url =
                    $"/api/v1/devicestatus.json?find[created_at][$gte]={startTime:yyyy-MM-ddTHH:mm:ss.fffZ}&count={count}";

                _logger.LogDebug("Fetching Nightscout device status: {Url}", url);

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError(
                        "Failed to fetch Nightscout device status: {StatusCode} - {Error}",
                        response.StatusCode,
                        errorContent
                    );
                    return Enumerable.Empty<DeviceStatus>();
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var deviceStatus = JsonSerializer.Deserialize<DeviceStatus[]>(jsonContent);

                if (deviceStatus == null || deviceStatus.Length == 0)
                {
                    _logger.LogDebug("No device status returned from source Nightscout");
                    return Enumerable.Empty<DeviceStatus>();
                }

                var filteredDeviceStatus = deviceStatus
                    .Where(status =>
                        status != null
                        && (
                            !since.HasValue
                            || (
                                status.CreatedAt != null
                                && DateTime.Parse(status.CreatedAt) > since.Value
                            )
                        )
                    )
                    .OrderBy(status => DateTime.Parse(status.CreatedAt!))
                    .ToList();

                _logger.LogInformation(
                    "Successfully fetched {Count} device status entries from source Nightscout",
                    filteredDeviceStatus.Count
                );
                return filteredDeviceStatus;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching device status from source Nightscout");
                return Enumerable.Empty<DeviceStatus>();
            }
        }

        /// <summary>
        /// Upload device status to target Nightscout
        /// </summary>
        public async Task<bool> UploadDeviceStatusToNightscoutAsync(
            IEnumerable<DeviceStatus> deviceStatusEntries,
            NightscoutConnectorConfiguration config
        )
        {
            try
            {
                var nightscoutUrl = config.NightscoutUrl.TrimEnd('/');
                var apiSecret = config.NightscoutApiSecret ?? config.ApiSecret;

                if (string.IsNullOrEmpty(apiSecret))
                {
                    throw new ArgumentException("API Secret is required for Nightscout upload");
                }

                var statusArray = deviceStatusEntries.ToList();

                if (statusArray.Count == 0)
                {
                    _logger.LogDebug("No device status entries to upload");
                    return true;
                }

                var json = JsonSerializer.Serialize(statusArray);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{nightscoutUrl}/api/v1/devicestatus"
                );
                request.Content = content;
                request.Headers.Add("API-SECRET", apiSecret);
                request.Headers.Add("User-Agent", "Nocturne-Connect/1.0");

                _logger.LogInformation(
                    "Uploading {Count} device status entries to {NightscoutUrl}",
                    statusArray.Count,
                    nightscoutUrl
                );

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "Successfully uploaded {Count} device status entries",
                        statusArray.Count
                    );
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError(
                        "Failed to upload device status entries: {StatusCode} - {Error}",
                        response.StatusCode,
                        errorContent
                    );
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading device status to Nightscout");
                return false;
            }
        }

        /// <summary>
        /// Syncs Nightscout data using the API data submitter
        /// </summary>
        /// <param name="config">Connector configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if sync was successful</returns>
        public async Task<bool> SyncNightscoutDataAsync(
            NightscoutConnectorConfiguration config,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                _logger.LogInformation("Starting Nightscout data sync using API data submitter");

                // Use the base class SyncDataAsync method which handles uploading to Nocturne API
                var success = await SyncDataAsync(config, cancellationToken);

                if (success)
                {
                    _logger.LogInformation("Nightscout data sync completed successfully");
                }
                else
                {
                    _logger.LogWarning("Nightscout data sync failed");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Nightscout data sync");
                return false;
            }
        }

        private static long ToUnixTimestamp(DateTime dateTime)
        {
            return ((DateTimeOffset)dateTime).ToUnixTimeMilliseconds();
        }
    }
}
