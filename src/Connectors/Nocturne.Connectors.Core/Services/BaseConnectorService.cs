using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Core.Models;

#nullable enable

namespace Nocturne.Connectors.Core.Services
{
    /// <summary>
    /// Base implementation for connector services with common Nightscout upload functionality
    /// </summary>
    /// <typeparam name="TConfig">The connector-specific configuration type</typeparam>
    public abstract class BaseConnectorService<TConfig> : IConnectorService<TConfig>
        where TConfig : IConnectorConfiguration
    {
        protected readonly HttpClient _httpClient;
        protected readonly IApiDataSubmitter? _apiDataSubmitter;
        protected readonly ILogger? _logger;
        private readonly bool _httpClientOwned;
        private const int MaxRetries = 3;
        private static readonly TimeSpan[] RetryDelays =
        {
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(30),
        };

        /// <summary>
        /// Unique identifier for this connector service type
        /// </summary>
        public abstract string ConnectorSource { get; }

        protected BaseConnectorService()
        {
            var handler = new HttpClientHandler()
            {
                AutomaticDecompression =
                    System.Net.DecompressionMethods.GZip
                    | System.Net.DecompressionMethods.Deflate
                    | System.Net.DecompressionMethods.Brotli,
            };
            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromMinutes(2); // 2-minute timeout
            _httpClientOwned = true;
            _apiDataSubmitter = null;
            _logger = null;
        }

        protected BaseConnectorService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _httpClientOwned = false;
            _apiDataSubmitter = null;
            _logger = null;
        }

        protected BaseConnectorService(
            HttpClient httpClient,
            IApiDataSubmitter? apiDataSubmitter,
            ILogger? logger
        )
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _httpClientOwned = false;
            _apiDataSubmitter = apiDataSubmitter;
            _logger = logger;
        }

        protected BaseConnectorService(IApiDataSubmitter? apiDataSubmitter, ILogger? logger)
        {
            var handler = new HttpClientHandler()
            {
                AutomaticDecompression =
                    System.Net.DecompressionMethods.GZip
                    | System.Net.DecompressionMethods.Deflate
                    | System.Net.DecompressionMethods.Brotli,
            };
            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromMinutes(2); // 2-minute timeout
            _httpClientOwned = true;
            _apiDataSubmitter = apiDataSubmitter;
            _logger = logger;
        }

        /// <summary>
        /// Get the timestamp of the most recent treatment in the target Nightscout instance
        /// This enables "catch up" functionality to fetch only new data since the last upload
        /// </summary>
        protected virtual async Task<DateTime?> FetchLatestTreatmentTimestampAsync(TConfig config)
        {
            try
            {
                // In Nocturne mode, we cannot query Nightscout directly
                if (config.Mode == ConnectorMode.Nocturne)
                {
                    Console.WriteLine(
                        "Warning: Cannot query Nightscout in Nocturne mode, using default lookback"
                    );
                    return null;
                }

                var nightscoutUrl = config.NightscoutUrl.TrimEnd('/');
                var apiSecret = !string.IsNullOrEmpty(config.NightscoutApiSecret)
                    ? config.NightscoutApiSecret
                    : config.ApiSecret;

                if (string.IsNullOrEmpty(nightscoutUrl) || string.IsNullOrEmpty(apiSecret))
                {
                    Console.WriteLine(
                        "Warning: No Nightscout URL or API secret provided for query, using default lookback"
                    );
                    return null;
                }

                // Hash the API secret to match Nightscout's expected format
                var hashedApiSecret = HashApiSecret(apiSecret);

                // Query for the most recent treatment from this connector source (count=50 to filter by source)
                var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{nightscoutUrl}/api/v1/treatments.json?count=50"
                );
                request.Headers.Add("API-SECRET", hashedApiSecret);
                request.Headers.Add("User-Agent", "Nocturne-Connect/1.0");

                Console.WriteLine(
                    $"Querying target Nightscout for most recent {ConnectorSource} treatment timestamp..."
                );

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var treatments = JsonSerializer.Deserialize<Treatment[]>(jsonContent);

                    if (treatments != null && treatments.Length > 0)
                    {
                        // Filter treatments to only those from this connector source
                        var connectorTreatments = treatments
                            .Where(t =>
                                !string.IsNullOrEmpty(t.Source)
                                && t.Source.Equals(
                                    ConnectorSource,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            .ToArray();

                        if (connectorTreatments.Length > 0)
                        {
                            var mostRecentTreatment = connectorTreatments[0];
                            if (DateTime.TryParse(mostRecentTreatment.CreatedAt, out var timestamp))
                            {
                                // Validate timestamp is not in the future
                                var now = DateTime.UtcNow;
                                if (timestamp > now)
                                {
                                    Console.WriteLine(
                                        $"Warning: Found treatment with future timestamp ({timestamp:yyyy-MM-dd HH:mm:ss} UTC > {now:yyyy-MM-dd HH:mm:ss} UTC), using default lookback"
                                    );
                                    return null;
                                }

                                Console.WriteLine(
                                    $"Found most recent {ConnectorSource} treatment at: {timestamp:yyyy-MM-dd HH:mm:ss} UTC"
                                );
                                return timestamp;
                            }
                            else
                            {
                                Console.WriteLine(
                                    $"Could not parse timestamp from most recent {ConnectorSource} treatment: {mostRecentTreatment.CreatedAt}"
                                );
                                return null;
                            }
                        }
                        else
                        {
                            Console.WriteLine(
                                $"No {ConnectorSource} treatments found in target Nightscout, using default lookback"
                            );
                            return null;
                        }
                    }
                    else
                    {
                        Console.WriteLine(
                            "No treatments found in target Nightscout, using default lookback"
                        );
                        return null;
                    }
                }
                else
                {
                    Console.WriteLine($"Failed to query target Nightscout: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Error querying target Nightscout for recent treatments: {ex.Message}"
                );
                return null;
            }
        }

        /// <summary>
        /// Calculate the optimal "since" timestamp for data fetching
        /// Uses catch-up logic to fetch from the most recent treatment, or falls back to default lookback
        /// </summary>
        protected virtual async Task<DateTime> CalculateSinceTimestampAsync(
            TConfig config,
            DateTime? defaultSince = null
        )
        {
            // First try to get the most recent treatment timestamp from target Nightscout
            var mostRecentTimestamp = await FetchLatestTreatmentTimestampAsync(config);

            if (mostRecentTimestamp.HasValue)
            {
                // Add a small overlap to ensure we don't miss any entries due to timing issues
                var sinceWithOverlap = mostRecentTimestamp.Value.AddMinutes(-5);

                // Ensure we don't go too far back (maximum 7 days for safety)
                var maxLookback = DateTime.UtcNow.AddDays(-7);
                if (sinceWithOverlap < maxLookback)
                {
                    Console.WriteLine(
                        $"Most recent treatment is older than 7 days, limiting lookback to 7 days"
                    );
                    return maxLookback;
                }

                Console.WriteLine(
                    $"Using catch-up mode: fetching data since {sinceWithOverlap:yyyy-MM-dd HH:mm:ss} UTC"
                );
                return sinceWithOverlap;
            }

            // Fallback to provided default or 24 hours
            var fallbackSince = defaultSince ?? DateTime.UtcNow.AddHours(-24);
            Console.WriteLine(
                $"Using fallback mode: fetching data since {fallbackSince:yyyy-MM-dd HH:mm:ss} UTC"
            );
            return fallbackSince;
        }

        /// <summary>
        /// Hash API secret using SHA1 to match Nightscout's expected format
        /// </summary>
        private static string HashApiSecret(string apiSecret)
        {
            using var sha1 = SHA1.Create();
            var hashBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(apiSecret));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        public abstract string ServiceName { get; }

        public abstract Task<bool> AuthenticateAsync();
        public abstract Task<IEnumerable<Entry>> FetchGlucoseDataAsync(DateTime? since = null);

        /// <summary>
        /// Submits glucose data directly to the API via HTTP
        /// </summary>
        protected virtual async Task<bool> PublishGlucoseDataAsync(
            IEnumerable<Entry> entries,
            TConfig config,
            CancellationToken cancellationToken = default
        )
        {
            if (_apiDataSubmitter == null)
            {
                _logger?.LogWarning("API data submitter not available for glucose data submission");
                return false;
            }

            var entriesArray = entries.ToArray();
            if (entriesArray.Length == 0)
            {
                _logger?.LogInformation("No glucose entries to submit");
                return true;
            }

            try
            {
                var success = await _apiDataSubmitter.SubmitEntriesAsync(
                    entriesArray,
                    ConnectorSource,
                    cancellationToken
                );

                if (success)
                {
                    _logger?.LogInformation(
                        "Successfully submitted {Count} glucose entries",
                        entriesArray.Length
                    );
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to submit glucose data");
                return false;
            }
        }

        /// <summary>
        /// Submits treatment data directly to the API via HTTP
        /// </summary>
        protected virtual async Task<bool> PublishTreatmentDataAsync(
            IEnumerable<Treatment> treatments,
            TConfig config,
            CancellationToken cancellationToken = default
        )
        {
            if (_apiDataSubmitter == null)
            {
                _logger?.LogWarning("API data submitter not available for treatment data submission");
                return false;
            }

            var treatmentsArray = treatments.ToArray();
            if (treatmentsArray.Length == 0)
            {
                _logger?.LogInformation("No treatments to submit");
                return true;
            }

            try
            {
                var success = await _apiDataSubmitter.SubmitTreatmentsAsync(
                    treatmentsArray,
                    ConnectorSource,
                    cancellationToken
                );

                if (success)
                {
                    _logger?.LogInformation(
                        "Successfully submitted {Count} treatments",
                        treatmentsArray.Length
                    );
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to submit treatment data");
                return false;
            }
        }

        /// <summary>
        /// Submits device status data directly to the API via HTTP
        /// </summary>
        protected virtual async Task<bool> PublishDeviceStatusAsync(
            IEnumerable<DeviceStatus> deviceStatuses,
            TConfig config,
            CancellationToken cancellationToken = default
        )
        {
            if (_apiDataSubmitter == null)
            {
                _logger?.LogWarning("API data submitter not available for device status submission");
                return false;
            }

            var statusArray = deviceStatuses.ToArray();
            if (statusArray.Length == 0)
            {
                _logger?.LogInformation("No device statuses to submit");
                return true;
            }

            try
            {
                var success = await _apiDataSubmitter.SubmitDeviceStatusAsync(
                    statusArray,
                    ConnectorSource,
                    cancellationToken
                );

                if (success)
                {
                    _logger?.LogInformation(
                        "Successfully submitted {Count} device statuses",
                        statusArray.Length
                    );
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to submit device status data");
                return false;
            }
        }

        /// <summary>
        /// Submits health data (comprehensive data from sources like Glooko)
        /// Currently only submits blood glucose readings as entries
        /// </summary>
        protected virtual async Task<bool> PublishHealthDataAsync(
            Entry[] bloodGlucoseReadings,
            object[] bloodPressureReadings,
            object[] weightReadings,
            object[] sleepReadings,
            TConfig config,
            CancellationToken cancellationToken = default
        )
        {
            if (_apiDataSubmitter == null)
            {
                _logger?.LogWarning("API data submitter not available for health data submission");
                return false;
            }

            // Submit blood glucose readings as entries
            if (bloodGlucoseReadings != null && bloodGlucoseReadings.Length > 0)
            {
                try
                {
                    var success = await _apiDataSubmitter.SubmitEntriesAsync(
                        bloodGlucoseReadings,
                        ConnectorSource,
                        cancellationToken
                    );

                    if (success)
                    {
                        _logger?.LogInformation(
                            "Successfully submitted {Count} blood glucose readings from health data",
                            bloodGlucoseReadings.Length
                        );
                    }

                    return success;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to submit health data");
                    return false;
                }
            }

            _logger?.LogInformation("No blood glucose readings in health data to submit");
            return true;
        }

        /// <summary>
        /// Publishes messages in batches to optimize throughput
        /// </summary>
        protected virtual async Task<bool> PublishGlucoseDataInBatchesAsync(
            IEnumerable<Entry> entries,
            TConfig config,
            CancellationToken cancellationToken = default
        )
        {
            var entriesArray = entries.ToArray();
            if (entriesArray.Length == 0)
            {
                return true;
            }

            var batchSize = Math.Max(1, config.BatchSize);
            var batches = entriesArray
                .Select((entry, index) => new { entry, index })
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.Select(x => x.entry).ToArray());

            bool allSuccessful = true;
            int batchNumber = 1;

            foreach (var batch in batches)
            {
                _logger?.LogDebug(
                    "Publishing batch {BatchNumber} with {Count} entries",
                    batchNumber,
                    batch.Length
                );

                var success = await PublishGlucoseDataAsync(batch, config, cancellationToken);
                if (!success)
                {
                    allSuccessful = false;
                    _logger?.LogWarning("Failed to publish batch {BatchNumber}", batchNumber);
                }

                batchNumber++;

                // Small delay between batches to avoid overwhelming the message bus
                if (batchNumber > 1)
                {
                    await Task.Delay(100, cancellationToken);
                }
            }

            return allSuccessful;
        }


        /// <summary>
        /// Main sync method that handles data synchronization based on connector mode
        /// </summary>
        public virtual async Task<bool> SyncDataAsync(
            TConfig config,
            CancellationToken cancellationToken = default
        )
        {
            // In Nocturne mode, only use message bus (no fallback to direct API)
            if (config.Mode == ConnectorMode.Nocturne)
            {
                if (_apiDataSubmitter == null)
                {
                    _logger?.LogError(
                        "API data submitter is required for Nocturne mode but is not available"
                    );
                    return false;
                }

                try
                {
                    _logger?.LogInformation(
                        "Using API data submitter for data synchronization from {ConnectorSource} in Nocturne mode",
                        ConnectorSource
                    );

                    // Fetch glucose data (use default since for Nocturne mode as no Nightscout lookup available)
                    var sinceTimestamp = DateTime.UtcNow.AddHours(-24);
                    var entries = await FetchGlucoseDataAsync(sinceTimestamp);

                    // Submit via API with batching
                    var success = await PublishGlucoseDataInBatchesAsync(
                        entries,
                        config,
                        cancellationToken
                    );

                    if (success)
                    {
                        _logger?.LogInformation(
                            "Successfully submitted data via API in Nocturne mode"
                        );
                        return true;
                    }
                    else
                    {
                        _logger?.LogError("API data submission failed in Nocturne mode");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "API data submission failed in Nocturne mode");
                    return false;
                }
            }

            // Standalone mode - prefer API submitter when available, fallback to direct API
            if (_apiDataSubmitter != null)
            {
                try
                {
                    _logger?.LogInformation(
                        "Using API data submitter for data synchronization from {ConnectorSource} in Standalone mode",
                        ConnectorSource
                    );

                    // Fetch glucose data
                    var sinceTimestamp = await CalculateSinceTimestampAsync(config);
                    var entries = await FetchGlucoseDataAsync(sinceTimestamp);

                    // Publish via message bus with batching
                    var success = await PublishGlucoseDataInBatchesAsync(
                        entries,
                        config,
                        cancellationToken
                    );

                    if (success)
                    {
                        _logger?.LogInformation("Successfully published data via message bus");
                        return true;
                    }
                    else if (config.FallbackToDirectApi)
                    {
                        _logger?.LogWarning(
                            "Message publishing failed, falling back to direct API upload"
                        );
                        return await UploadToNightscoutAsync(entries, config);
                    }
                    else
                    {
                        _logger?.LogError("Message publishing failed and fallback disabled");
                        return false;
                    }
                }
                catch (Exception ex) when (config.FallbackToDirectApi)
                {
                    _logger?.LogWarning(
                        ex,
                        "Message bus processing failed, falling back to direct API"
                    );
                    var sinceTimestamp = await CalculateSinceTimestampAsync(config);
                    var entries = await FetchGlucoseDataAsync(sinceTimestamp);
                    return await UploadToNightscoutAsync(entries, config);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Message bus processing failed and fallback disabled");
                    return false;
                }
            }
            else
            {
                _logger?.LogInformation(
                    "Message bus not available, using direct API processing for {ConnectorSource} in Standalone mode",
                    ConnectorSource
                );
                var sinceTimestamp = await CalculateSinceTimestampAsync(config);
                var entries = await FetchGlucoseDataAsync(sinceTimestamp);
                return await UploadToNightscoutAsync(entries, config);
            }
        }

        /// <summary>
        /// Upload glucose entries to Nightscout using the entries API
        /// </summary>
        public virtual async Task<bool> UploadToNightscoutAsync(
            IEnumerable<Entry> entries,
            TConfig config
        )
        {
            try
            {
                // In Nocturne mode, direct upload should not be used
                if (config.Mode == ConnectorMode.Nocturne)
                {
                    _logger?.LogWarning(
                        "Direct Nightscout upload attempted in Nocturne mode - this should use message bus instead"
                    );
                    return false;
                }

                var nightscoutUrl = config.NightscoutUrl.TrimEnd('/');
                var apiSecret = !string.IsNullOrEmpty(config.NightscoutApiSecret)
                    ? config.NightscoutApiSecret
                    : config.ApiSecret;

                if (string.IsNullOrEmpty(nightscoutUrl))
                {
                    throw new ArgumentException("Nightscout URL is required for direct upload");
                }

                if (string.IsNullOrEmpty(apiSecret))
                {
                    throw new ArgumentException("API Secret is required for Nightscout upload");
                }
                var entriesArray = new List<object>();
                foreach (var entry in entries)
                {
                    entriesArray.Add(entry);
                }

                if (entriesArray.Count == 0)
                {
                    Console.WriteLine("No entries to upload");
                    return true;
                }

                // Split into batches of 100 entries each to avoid large requests
                const int batchSize = 100;
                var batches = entriesArray
                    .Select((entry, index) => new { entry, index })
                    .GroupBy(x => x.index / batchSize)
                    .Select(g => g.Select(x => x.entry).ToList())
                    .ToList();

                bool allSuccessful = true;

                foreach (var batch in batches)
                {
                    var success = await UploadBatchToNightscoutAsync(
                        batch,
                        nightscoutUrl,
                        apiSecret
                    );
                    if (!success)
                    {
                        allSuccessful = false;
                    }
                }

                return allSuccessful;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading to Nightscout: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> UploadBatchToNightscoutAsync(
            List<object> batch,
            string nightscoutUrl,
            string apiSecret
        )
        {
            const int maxRetries = 3;
            var retryDelays = new[]
            {
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(15),
            };

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var json = JsonSerializer.Serialize(batch);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    // Hash the API secret to match Nightscout's expected format
                    var hashedApiSecret = HashApiSecret(apiSecret);

                    var request = new HttpRequestMessage(
                        HttpMethod.Post,
                        $"{nightscoutUrl}/api/v1/entries"
                    );
                    request.Content = content;
                    request.Headers.Add("API-SECRET", hashedApiSecret);
                    request.Headers.Add("User-Agent", "Nocturne-Connect/1.0");

                    Console.WriteLine(
                        $"Uploading batch of {batch.Count} entries to {nightscoutUrl} (attempt {attempt + 1})"
                    );

                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Successfully uploaded batch of {batch.Count} entries");
                        return true;
                    }
                    else if (IsRetryableStatusCode(response.StatusCode) && attempt < maxRetries)
                    {
                        // Retryable error - wait and retry
                        Console.WriteLine(
                            $"Retryable error {response.StatusCode}, waiting {retryDelays[attempt].TotalSeconds} seconds before retry"
                        );
                        await Task.Delay(retryDelays[attempt]);
                        continue;
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine(
                            $"Failed to upload batch: {response.StatusCode} - {errorContent}"
                        );
                        return false;
                    }
                }
                catch (HttpRequestException ex) when (attempt < maxRetries)
                {
                    Console.WriteLine(
                        $"HTTP error on attempt {attempt + 1}: {ex.Message}, retrying..."
                    );
                    await Task.Delay(retryDelays[attempt]);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error uploading batch to Nightscout: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Upload treatments to Nightscout using the treatments API
        /// </summary>
        public virtual async Task<bool> UploadTreatmentsToNightscoutAsync(
            IEnumerable<Treatment> treatments,
            TConfig config
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

                var treatmentsArray = treatments.ToList();

                if (treatmentsArray.Count == 0)
                {
                    Console.WriteLine("No treatments to upload");
                    return true;
                }
                var json = JsonSerializer.Serialize(treatmentsArray);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Hash the API secret to match Nightscout's expected format
                var hashedApiSecret = HashApiSecret(apiSecret);

                var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{nightscoutUrl}/api/v1/treatments"
                );
                request.Content = content;
                request.Headers.Add("API-SECRET", hashedApiSecret);
                request.Headers.Add("User-Agent", "Nocturne-Connect/1.0");

                Console.WriteLine(
                    $"Uploading {treatmentsArray.Count} treatments to {nightscoutUrl}"
                );

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Successfully uploaded {treatmentsArray.Count} treatments");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(
                        $"Failed to upload treatments: {response.StatusCode} - {errorContent}"
                    );
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading treatments to Nightscout: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Optional method for connectors to implement file-based data loading/saving for debugging
        /// </summary>
        /// <typeparam name="TData">The type of data to save/load</typeparam>
        /// <param name="config">Configuration containing file I/O settings</param>
        /// <param name="dataFetcher">Function to fetch fresh data from the API</param>        /// <param name="dataProcessor">Function to process the data into glucose entries</param>
        /// <param name="fileService">File service for saving/loading data</param>
        /// <param name="filePrefix">Prefix for data files (e.g., "glooko_batch")</param>
        /// <param name="since">Optional since parameter for data fetching</param>
        /// <returns>Processed glucose entries</returns>
        protected virtual async Task<IEnumerable<Entry>> FetchWithOptionalFileIOAsync<TData>(
            TConfig config,
            Func<DateTime?, Task<TData?>> dataFetcher,
            Func<TData, IEnumerable<Entry>> dataProcessor,
            IConnectorFileService<TData> fileService,
            string filePrefix,
            DateTime? since = null
        )
            where TData : class
        {
            var entries = new List<Entry>();

            try
            {
                TData? data = null;

                // Check if we should load from file instead of fetching from API
                if (config.LoadFromFile)
                {
                    if (!string.IsNullOrEmpty(config.LoadFilePath))
                    {
                        Console.WriteLine(
                            $"Loading {filePrefix} data from specified file: {config.LoadFilePath}"
                        );
                        data = await fileService.LoadDataAsync(config.LoadFilePath);
                    }
                    else
                    {
                        // Load from most recent file in data directory
                        var mostRecentFile = fileService.GetMostRecentDataFile(
                            config.DataDirectory,
                            filePrefix
                        );
                        if (mostRecentFile != null)
                        {
                            Console.WriteLine(
                                $"Loading {filePrefix} data from most recent file: {mostRecentFile}"
                            );
                            data = await fileService.LoadDataAsync(mostRecentFile);
                        }
                        else
                        {
                            Console.WriteLine(
                                $"No saved {filePrefix} data files found in directory: {config.DataDirectory}"
                            );
                        }
                    }
                }

                // If no data loaded from file, fetch from API
                if (data == null)
                {
                    Console.WriteLine($"Fetching fresh {filePrefix} data from API");
                    data = await dataFetcher(since);

                    // Save the fetched data if SaveRawData is enabled
                    if (data != null && config.SaveRawData)
                    {
                        var savedPath = await fileService.SaveDataAsync(
                            data,
                            config.DataDirectory,
                            filePrefix
                        );
                        if (savedPath != null)
                        {
                            Console.WriteLine(
                                $"Saved {filePrefix} data for debugging: {savedPath}"
                            );
                        }
                    }
                }

                // Process data into glucose entries
                if (data != null)
                {
                    entries.AddRange(dataProcessor(data));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in FetchWithOptionalFileIOAsync: {ex.Message}");
                throw;
            }

            return entries;
        }

        /// <summary>
        /// Save raw data files by data type to organized folder structure
        /// </summary>
        protected async Task SaveDataByTypeAsync<T>(
            T[] data,
            string dataTypeName,
            string connectorName,
            TConfig config,
            ILogger logger
        )
        {
            if (data == null || data.Length == 0)
                return;

            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd");
                var connectorFolder = Path.Combine(
                    config.DataDirectory,
                    connectorName.ToLowerInvariant()
                );
                var fileName =
                    $"{connectorName.ToLowerInvariant()}-{timestamp}-{dataTypeName.ToLowerInvariant()}.json";

                // Ensure directory exists
                if (!Directory.Exists(connectorFolder))
                {
                    Directory.CreateDirectory(connectorFolder);
                    logger.LogInformation(
                        "Created connector directory: {ConnectorFolder}",
                        connectorFolder
                    );
                }

                var filePath = Path.Combine(connectorFolder, fileName);
                var json = JsonSerializer.Serialize(
                    data,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    }
                );

                await File.WriteAllTextAsync(filePath, json);
                logger.LogInformation(
                    "Saved {Count} {DataType} entries to {FilePath}",
                    data.Length,
                    dataTypeName,
                    filePath
                );
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "Error saving {DataType} data to file: {Error}",
                    dataTypeName,
                    ex.Message
                );
            }
        }

        /// <summary>
        /// Save treatments to file with organized folder structure
        /// </summary>
        protected async Task SaveTreatmentsToFileAsync(
            IEnumerable<Treatment> treatments,
            string connectorName,
            TConfig config,
            ILogger logger
        )
        {
            if (treatments == null)
                return;

            var treatmentsList = treatments.ToList();
            if (treatmentsList.Count == 0)
                return;

            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd");
                var connectorFolder = Path.Combine(
                    config.DataDirectory,
                    connectorName.ToLowerInvariant()
                );
                var fileName = $"{connectorName.ToLowerInvariant()}-{timestamp}-treatments.json";

                // Ensure directory exists
                if (!Directory.Exists(connectorFolder))
                {
                    Directory.CreateDirectory(connectorFolder);
                    logger.LogInformation(
                        "Created connector directory: {ConnectorFolder}",
                        connectorFolder
                    );
                }

                var filePath = Path.Combine(connectorFolder, fileName);
                var json = JsonSerializer.Serialize(
                    treatmentsList,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    }
                );

                await File.WriteAllTextAsync(filePath, json);
                logger.LogInformation(
                    "Saved {Count} treatments to {FilePath}",
                    treatmentsList.Count,
                    filePath
                );
            }
            catch (Exception ex)
            {
                logger.LogError("Error saving treatments to file: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Save glucose entries to file with organized folder structure
        /// </summary>
        protected async Task SaveGlucoseEntriesToFileAsync(
            IEnumerable<Entry> entries,
            string connectorName,
            TConfig config,
            ILogger logger
        )
        {
            if (entries == null)
                return;

            var entriesList = entries.ToList();
            if (entriesList.Count == 0)
                return;

            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd");
                var connectorFolder = Path.Combine(
                    config.DataDirectory,
                    connectorName.ToLowerInvariant()
                );
                var fileName = $"{connectorName.ToLowerInvariant()}-{timestamp}-glucose.json";

                // Ensure directory exists
                if (!Directory.Exists(connectorFolder))
                {
                    Directory.CreateDirectory(connectorFolder);
                    logger.LogInformation(
                        "Created connector directory: {ConnectorFolder}",
                        connectorFolder
                    );
                }

                var filePath = Path.Combine(connectorFolder, fileName);
                var json = JsonSerializer.Serialize(
                    entriesList,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    }
                );

                await File.WriteAllTextAsync(filePath, json);
                logger.LogInformation(
                    "Saved {Count} glucose entries to {FilePath}",
                    entriesList.Count,
                    filePath
                );
            }
            catch (Exception ex)
            {
                logger.LogError("Error saving glucose entries to file: {Error}", ex.Message);
            }
        }

        private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.InternalServerError
                || statusCode == HttpStatusCode.BadGateway
                || statusCode == HttpStatusCode.ServiceUnavailable
                || statusCode == HttpStatusCode.GatewayTimeout
                || statusCode == HttpStatusCode.TooManyRequests
                || statusCode == HttpStatusCode.RequestTimeout;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && _httpClientOwned)
            {
                _httpClient?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
