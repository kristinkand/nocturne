using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.Connectors.Configurations;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.Core.Services;
using Nocturne.Connectors.Core.Utilities;
using Nocturne.Connectors.Dexcom.Constants;
using Nocturne.Core.Constants;
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
        private readonly IRetryDelayStrategy _retryDelayStrategy;
        private readonly IRateLimitingStrategy _rateLimitingStrategy;
        private readonly IAuthTokenProvider _tokenProvider;
        private readonly IConnectorFileService<DexcomEntry[]>? _fileService = null;

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

        public override string ConnectorSource => DataSources.DexcomConnector;
        public override string ServiceName => "Dexcom Share";
        public override List<SyncDataType> SupportedDataTypes => [SyncDataType.Glucose];

        public DexcomConnectorService(
            HttpClient httpClient,
            IOptions<DexcomConnectorConfiguration> config,
            ILogger<DexcomConnectorService> logger,
            IRetryDelayStrategy retryDelayStrategy,
            IRateLimitingStrategy rateLimitingStrategy,
            IAuthTokenProvider tokenProvider,
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
            _tokenProvider =
                tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        }

        public override async Task<bool> AuthenticateAsync()
        {
            var token = await _tokenProvider.GetValidTokenAsync();
            if (token == null)
            {
                TrackFailedRequest("Failed to get valid token");
                return false;
            }

            TrackSuccessfulRequest();
            return true;
        }

        public override async Task<IEnumerable<Entry>> FetchGlucoseDataAsync(DateTime? since = null)
        {
            // Use the base class helper for file I/O and data fetching
            var entries = await FetchWithOptionalFileIOAsync(
                _config,
                async (s) => await FetchBatchDataAsync(s),
                TransformBatchDataToEntries,
                _fileService,
                "dexcom_batch",
                since
            );

            _logger.LogInformation(
                "[{ConnectorSource}] Retrieved {Count} glucose entries from Dexcom",
                ConnectorSource,
                entries.Count()
            );

            return entries;
        }

        private IEnumerable<Entry> TransformBatchDataToEntries(DexcomEntry[] batchData)
        {
            if (batchData == null || batchData.Length == 0)
            {
                return Enumerable.Empty<Entry>();
            }

            return batchData
                .Where(entry => entry != null && entry.Value > 0)
                .Select(ConvertDexcomEntry)
                .OrderBy(entry => entry.Date)
                .ToList();
        }

        private async Task<DexcomEntry[]?> FetchBatchDataAsync(DateTime? since = null)
        {
            // Get valid session token from provider
            var sessionId = await _tokenProvider.GetValidTokenAsync();
            if (string.IsNullOrEmpty(sessionId))
            {
                _logger.LogWarning(
                    "[{ConnectorSource}] Failed to get valid session, authentication failed",
                    ConnectorSource
                );
                TrackFailedRequest("Failed to get valid session");
                return null;
            }

            // Apply rate limiting
            await _rateLimitingStrategy.ApplyDelayAsync(0);

            var result = await ExecuteWithRetryAsync(
                async () => await FetchRawDataCoreAsync(sessionId, since),
                _retryDelayStrategy,
                reAuthenticateOnUnauthorized: async () =>
                {
                    _tokenProvider.InvalidateToken();
                    var newToken = await _tokenProvider.GetValidTokenAsync();
                    if (!string.IsNullOrEmpty(newToken))
                    {
                        sessionId = newToken;
                        return true;
                    }
                    return false;
                },
                operationName: "FetchDexcomData"
            );

            // Log batch data summary
            if (result != null)
            {
                var validEntries = result.Where(e => e != null && e.Value > 0).ToArray();
                var minDate = validEntries.Length > 0 ? validEntries.Min(e => e.WT) : "N/A";
                var maxDate = validEntries.Length > 0 ? validEntries.Max(e => e.WT) : "N/A";

                _logger.LogInformation(
                    "[{ConnectorSource}] Fetched Dexcom batch data: TotalEntries={TotalCount}, ValidEntries={ValidCount}, DateRange={MinDate} to {MaxDate}",
                    ConnectorSource,
                    result.Length,
                    validEntries.Length,
                    minDate,
                    maxDate
                );
            }

            return result;
        }

        /// <summary>
        /// Core data fetch logic without retry handling (called by ExecuteWithRetryAsync)
        /// </summary>
        private async Task<DexcomEntry[]?> FetchRawDataCoreAsync(
            string sessionId,
            DateTime? since = null
        )
        {
            // Calculate time range
            var twoDaysAgo = DateTime.UtcNow.AddDays(-2);
            var startTime = since.HasValue
                ? (since.Value > twoDaysAgo ? since.Value : twoDaysAgo)
                : twoDaysAgo;

            var timeDiff = DateTime.UtcNow - startTime;
            var maxCount = Math.Ceiling(timeDiff.TotalMinutes / 5); // 5-minute intervals
            var minutes = (int)(maxCount * 5);

            var url =
                $"/ShareWebServices/Services/Publisher/ReadPublisherLatestGlucoseValues?sessionID={sessionId}&minutes={minutes}&maxCount={(int)maxCount}";

            var response = await _httpClient.PostAsync(
                url,
                new StringContent("{}", Encoding.UTF8, "application/json")
            );

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"HTTP {(int)response.StatusCode} {response.StatusCode}: {errorContent}",
                    null,
                    response.StatusCode
                );
            }

            var dexcomEntries = await DeserializeResponseAsync<DexcomEntry[]>(response);
            return dexcomEntries ?? [];
        }

        private Entry ConvertDexcomEntry(DexcomEntry dexcomEntry)
        {
            try
            {
                // Parse Dexcom's date format using shared utility
                if (!TimestampParser.TryParseDexcomFormat(dexcomEntry.WT, out var timestamp))
                {
                    _logger.LogWarning(
                        "Could not parse Dexcom timestamp: {Timestamp}",
                        dexcomEntry.WT
                    );
                    return new Entry { Type = "sgv", Device = ConnectorSource };
                }

                var direction = TrendDirections.GetValueOrDefault(
                    dexcomEntry.Trend,
                    Direction.NotComputable
                );

                return new Entry
                {
                    Date = timestamp,
                    Sgv = dexcomEntry.Value,
                    Direction = direction.ToString(),
                    Device = ConnectorSource,
                    Type = "sgv",
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error converting Dexcom entry: {@Entry}", dexcomEntry);
                return new Entry { Type = "sgv", Device = ConnectorSource };
            }
        }

        public class DexcomEntry
        {
            public string DT { get; set; } = string.Empty;
            public string ST { get; set; } = string.Empty;
            public int Trend { get; set; }
            public int Value { get; set; }
            public string WT { get; set; } = string.Empty;
        }
    }
}
