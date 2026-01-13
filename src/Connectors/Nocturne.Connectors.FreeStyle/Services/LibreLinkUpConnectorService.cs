using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nocturne.Connectors.Configurations;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.Core.Services;
using Nocturne.Connectors.Core.Utilities;
using Nocturne.Connectors.FreeStyle.Constants;
using Nocturne.Core.Constants;
using Nocturne.Core.Models;

#nullable enable

namespace Nocturne.Connectors.FreeStyle.Services
{
    /// <summary>
    /// Connector service for LibreLinkUp data source
    /// Enhanced implementation based on the original nightscout-connect LibreLinkUp implementation
    /// </summary>
    public class LibreConnectorService(
        HttpClient httpClient,
        IOptions<LibreLinkUpConnectorConfiguration> config,
        ILogger<LibreConnectorService> logger,
        IRetryDelayStrategy retryDelayStrategy,
        IRateLimitingStrategy rateLimitingStrategy,
        IAuthTokenProvider tokenProvider,
        IApiDataSubmitter? apiDataSubmitter = null,
        IConnectorMetricsTracker? metricsTracker = null,
        IConnectorStateService? stateService = null
    )
        : BaseConnectorService<LibreLinkUpConnectorConfiguration>(
            httpClient,
            logger,
            apiDataSubmitter,
            metricsTracker,
            stateService
        )
    {
        private readonly LibreLinkUpConnectorConfiguration _config =
            config?.Value ?? throw new ArgumentNullException(nameof(config));
        private readonly IRetryDelayStrategy _retryDelayStrategy =
            retryDelayStrategy ?? throw new ArgumentNullException(nameof(retryDelayStrategy));
        private readonly IRateLimitingStrategy _rateLimitingStrategy =
            rateLimitingStrategy ?? throw new ArgumentNullException(nameof(rateLimitingStrategy));
        private readonly IAuthTokenProvider _tokenProvider =
            tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        private LibreUserConnection? _selectedConnection;
        private string _accountIdHash = string.Empty;

        /// <summary>
        /// Custom headers to include in API requests (Account-Id for LibreLinkUp)
        /// </summary>
        private Dictionary<string, string>? RequestHeaders =>
            string.IsNullOrWhiteSpace(_accountIdHash)
                ? null
                : new() { { "Account-Id", _accountIdHash } };

        private static readonly Dictionary<string, string> KnownEndpoints = new()
        {
            { "AE", LibreLinkUpConstants.Endpoints.AE },
            { "AP", LibreLinkUpConstants.Endpoints.AP },
            { "AU", LibreLinkUpConstants.Endpoints.AU },
            { "CA", LibreLinkUpConstants.Endpoints.CA },
            { "DE", LibreLinkUpConstants.Endpoints.DE },
            { "EU", LibreLinkUpConstants.Endpoints.EU },
            { "EU2", LibreLinkUpConstants.Endpoints.EU2 },
            { "FR", LibreLinkUpConstants.Endpoints.FR },
            { "JP", LibreLinkUpConstants.Endpoints.JP },
            { "US", LibreLinkUpConstants.Endpoints.US },
        };

        private static readonly Dictionary<int, Direction> TrendArrowMap = new()
        {
            { 1, Direction.SingleDown },
            { 2, Direction.FortyFiveDown },
            { 3, Direction.Flat },
            { 4, Direction.FortyFiveUp },
            { 5, Direction.SingleUp },
        };

        public override string ServiceName => "LibreLinkUp";

        /// <summary>
        /// Gets the source identifier for this connector
        /// </summary>
        public override string ConnectorSource => DataSources.LibreConnector;

        public override List<SyncDataType> SupportedDataTypes => [SyncDataType.Glucose];

        public override async Task<bool> AuthenticateAsync()
        {
            var token = await _tokenProvider.GetValidTokenAsync();
            if (token == null)
            {
                _accountIdHash = string.Empty;
                TrackFailedRequest("Failed to get valid token");
                return false;
            }

            _accountIdHash = string.Empty;
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadToken(token) as JwtSecurityToken;
                if (jwt is null)
                {
                    _logger.LogWarning("LibreLinkUp token is not a valid JWT");
                }

                if (jwt is not null)
                {
                    var claim = jwt.Claims.FirstOrDefault(c => c.Type == "id");
                    if (claim?.Value is { Length: > 0 } value)
                    {
                        _accountIdHash = HashUtils.Sha256Hex(value);
                    }
                    if (_accountIdHash.Length == 0)
                    {
                        _logger.LogWarning("LibreLinkUp token missing id claim");
                    }
                }
            }
            catch (ArgumentException)
            {
                _logger.LogWarning("LibreLinkUp token is not a valid JWT");
            }

            // Set up authorization header for subsequent requests
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            // Get connections to find the patient data
            await LoadConnectionsAsync();

            TrackSuccessfulRequest();
            return true;
        }

        private async Task LoadConnectionsAsync()
        {
            try
            {
                var response = await GetWithHeadersAsync(
                    LibreLinkUpConstants.ApiPaths.Connections,
                    RequestHeaders
                );

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Failed to load LibreLinkUp connections: {StatusCode}",
                        response.StatusCode
                    );
                    return;
                }

                var connectionsResponse = await DeserializeResponseAsync<LibreConnectionsResponse>(
                    response
                );

                if (connectionsResponse?.Data == null || connectionsResponse.Data.Length == 0)
                {
                    _logger.LogWarning("No LibreLinkUp connections found");
                    return;
                }

                // Select the specified patient or the first available connection
                if (!string.IsNullOrEmpty(_config.LibrePatientId))
                {
                    _selectedConnection = connectionsResponse.Data.FirstOrDefault(c =>
                        c.PatientId == _config.LibrePatientId
                    );
                }

                if (_selectedConnection == null)
                {
                    _selectedConnection = connectionsResponse.Data.First();
                    _logger.LogInformation(
                        "Selected LibreLinkUp connection: {PatientName} ({PatientId})",
                        _selectedConnection.FirstName + " " + _selectedConnection.LastName,
                        _selectedConnection.PatientId
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading LibreLinkUp connections");
            }
        }

        public override async Task<IEnumerable<Entry>> FetchGlucoseDataAsync(DateTime? since = null)
        {
            // Check if we need to authenticate or re-authenticate
            if (_tokenProvider.IsTokenExpired || _selectedConnection == null)
            {
                _logger.LogInformation(
                    "Token expired or missing connection, attempting to re-authenticate"
                );
                if (!await AuthenticateAsync())
                {
                    _logger.LogError("Failed to authenticate with LibreLinkUp");
                    return Enumerable.Empty<Entry>();
                }
            }

            if (string.IsNullOrWhiteSpace(_selectedConnection?.PatientId))
            {
                _logger.LogError("Invalid LibreLinkUp patient id");
                TrackFailedRequest("Invalid patient id");
                return Enumerable.Empty<Entry>();
            }

            var url = string.Format(
                LibreLinkUpConstants.ApiPaths.GraphData,
                _selectedConnection.PatientId
            );

            // Apply rate limiting before first attempt
            await _rateLimitingStrategy.ApplyDelayAsync(0);

            var result = await ExecuteWithRetryAsync(
                async () => await FetchGlucoseDataCoreAsync(url, since),
                _retryDelayStrategy,
                reAuthenticateOnUnauthorized: async () =>
                {
                    _tokenProvider.InvalidateToken();
                    _selectedConnection = null;
                    return await AuthenticateAsync();
                },
                operationName: "FetchGlucoseData"
            );

            return result ?? Enumerable.Empty<Entry>();
        }

        /// <summary>
        /// Core glucose data fetch logic without retry handling (called by ExecuteWithRetryAsync)
        /// </summary>
        private async Task<List<Entry>?> FetchGlucoseDataCoreAsync(string url, DateTime? since)
        {
            var response = await GetWithHeadersAsync(url, RequestHeaders);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"HTTP {(int)response.StatusCode} {response.StatusCode}: {errorContent}",
                    null,
                    response.StatusCode
                );
            }

            var graphResponse = await DeserializeResponseAsync<LibreGraphResponse>(response);

            if (graphResponse?.Data?.GraphData == null || graphResponse.Data.GraphData.Length == 0)
            {
                _logger.LogDebug("No glucose data returned from LibreLinkUp");
                return [];
            }

            var measurements = graphResponse.Data.GraphData.ToList();
            var latestMeasurement = graphResponse.Data.Connection.GlucoseMeasurement;
            if (latestMeasurement != null)
            {
                measurements.Add(latestMeasurement);
            }

            var glucoseEntries = measurements
                .Where(measurement => measurement != null && measurement.ValueInMgPerDl > 0)
                .Select(ConvertLibreEntry)
                .Where(entry => entry != null && (!since.HasValue || entry.Date > since.Value))
                .OrderBy(entry => entry.Date)
                .ToList();

            _logger.LogInformation(
                "[{ConnectorSource}] Successfully fetched {Count} glucose entries from LibreLinkUp",
                ConnectorSource,
                glucoseEntries.Count
            );

            return glucoseEntries;
        }

        /// <summary>
        /// Override health check to also consider token expiry
        /// </summary>
        public override bool IsHealthy => base.IsHealthy && !_tokenProvider.IsTokenExpired;

        private Entry ConvertLibreEntry(LibreGlucoseMeasurement measurement)
        {
            try
            {
                var timestamp = TimestampParser.ParseLibreFormat(measurement.FactoryTimestamp);

                var direction = TrendArrowMap.GetValueOrDefault(
                    measurement.TrendArrow,
                    Direction.NotComputable
                );
                return new Entry
                {
                    Date = timestamp,
                    Sgv = measurement.ValueInMgPerDl,
                    Direction = direction.ToString(),
                    Device = LibreLinkUpConstants.Configuration.DeviceIdentifier,
                    Type = LibreLinkUpConstants.Configuration.EntryType,
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error converting LibreLinkUp entry: {@Entry}", measurement);
                return new Entry { Type = "sgv", Device = "nightscout-connect-libre-linkup" };
            }
        }

        private class LibreLoginResponse
        {
            public required LibreLoginData Data { get; set; }
        }

        private class LibreLoginData
        {
            public required LibreAuthTicket AuthTicket { get; set; }
        }

        private class LibreAuthTicket
        {
            public required string Token { get; set; }
        }

        private class LibreConnectionsResponse
        {
            public required LibreUserConnection[] Data { get; set; }
        }

        private class LibreUserConnection
        {
            public required string PatientId { get; set; }
            public required string FirstName { get; set; }
            public required string LastName { get; set; }
        }

        private class LibreGraphResponse
        {
            public required LibreConnectionData Data { get; set; }
        }

        private class LibreConnectionData
        {
            public required LibreConnection Connection { get; set; }
            public required LibreGlucoseMeasurement[] GraphData { get; set; }
        }

        private class LibreConnection
        {
            public required LibreGlucoseMeasurement GlucoseMeasurement { get; set; }
        }

        private class LibreGlucoseMeasurement
        {
            public required string FactoryTimestamp { get; set; }
            public int ValueInMgPerDl { get; set; }
            public int TrendArrow { get; set; }
        }
    }
}
