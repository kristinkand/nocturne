using System;
using System.Collections.Generic;
using System.Globalization;
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
using Nocturne.Connectors.MyFitnessPal.Models;
using Nocturne.Core.Models;
using Nocturne.Core.Constants;

namespace Nocturne.Connectors.MyFitnessPal.Services;

/// <summary>
/// Connector service for MyFitnessPal food diary data
/// Supports both direct integration via dependency injection and HTTP fallback
/// </summary>
public class MyFitnessPalConnectorService : BaseConnectorService<MyFitnessPalConnectorConfiguration>
{
    private readonly MyFitnessPalConnectorConfiguration _config;
    private readonly Func<
        IEnumerable<Food>,
        CancellationToken,
        Task<IEnumerable<Food>>
    >? _createFoodAsync;
    private const string MyFitnessPalApiUrlBase =
        "https://www.myfitnesspal.com/api/services/authenticate_diary_key";

    /// <summary>
    /// Gets the connector source identifier
    /// </summary>
    public override string ConnectorSource => DataSources.MyFitnessPalConnector;

    /// <summary>
    /// Gets the service name for this connector
    /// </summary>
    public override string ServiceName => "MyFitnessPal";

    public override List<SyncDataType> SupportedDataTypes => new() { SyncDataType.Food };

    public override async Task<SyncResult> SyncDataAsync(
        SyncRequest request,
        MyFitnessPalConnectorConfiguration config,
        CancellationToken cancellationToken
    )
    {
        return await SyncConnectorFoodEntriesAsync(request.From, request.To, cancellationToken);
    }

    public override async Task<bool> SyncDataAsync(
        MyFitnessPalConnectorConfiguration config,
        CancellationToken cancellationToken = default,
        DateTime? since = null
    )
    {
        _logger.LogInformation("Starting background food entry sync for {ConnectorSource}", ConnectorSource);
        _stateService?.SetState(ConnectorState.Syncing, "Syncing food entries...");

        try
        {
            var fromDate = since ?? DateTime.Today.AddDays(-config.SyncDays);
            var result = await SyncConnectorFoodEntriesAsync(fromDate, null, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("Background food entry sync completed for {ConnectorSource}", ConnectorSource);
                _stateService?.SetState(ConnectorState.Idle, "Sync completed successfully");
                return true;
            }

            _logger.LogError(
                "Background sync for {ConnectorSource} failed: {Errors}",
                ConnectorSource,
                string.Join("; ", result.Errors)
            );
            _stateService?.SetState(ConnectorState.Error, "Sync completed with errors");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error syncing {ConnectorSource}", ConnectorSource);
            _stateService?.SetState(ConnectorState.Error, $"Unexpected error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Fetches food data from MyFitnessPal
    /// </summary>
    protected override async Task<IEnumerable<Food>> FetchFoodsAsync(DateTime? from = null, DateTime? to = null)
    {
        var username = _config.MyFitnessPalUsername;
        if (string.IsNullOrWhiteSpace(username))
        {
            _logger.LogWarning("Cannot fetch MyFitnessPal data - Username not configured");
            return Enumerable.Empty<Food>();
        }

        var diaryResponse = await FetchDiaryAsync(username, from, to);
        return ConvertToNightscoutFoods(diaryResponse);
    }

    private async Task<SyncResult> SyncConnectorFoodEntriesAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken
    )
    {
        var result = new SyncResult
        {
            StartTime = DateTimeOffset.UtcNow,
            Success = true
        };

        try
        {
            if (!await AuthenticateAsync())
            {
                result.Success = false;
                result.Errors.Add("Authentication failed");
                result.EndTime = DateTimeOffset.UtcNow;
                return result;
            }

            if (_apiDataSubmitter == null)
            {
                _logger.LogWarning("API data submitter not available for connector food entries");
                result.Success = false;
                result.Errors.Add("API data submitter not available");
                result.EndTime = DateTimeOffset.UtcNow;
                return result;
            }

            var imports = await FetchConnectorFoodEntriesAsync(from, to);
            var importList = imports.ToList();

            var success = await _apiDataSubmitter.SubmitConnectorFoodEntriesAsync(
                importList,
                ConnectorSource,
                cancellationToken
            );

            result.ItemsSynced[SyncDataType.Food] = importList.Count;
            if (importList.Count > 0)
            {
                var latest = importList.Max(i => i.ConsumedAt);
                result.LastEntryTimes[SyncDataType.Food] = latest.UtcDateTime;
            }

            if (!success)
            {
                result.Success = false;
                result.Errors.Add("Failed to submit connector food entries");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync MyFitnessPal connector food entries");
            result.Success = false;
            result.Errors.Add($"Failed to sync connector food entries: {ex.Message}");
        }

        result.EndTime = DateTimeOffset.UtcNow;
        return result;
    }

    private async Task<IEnumerable<ConnectorFoodEntryImport>> FetchConnectorFoodEntriesAsync(
        DateTime? from = null,
        DateTime? to = null
    )
    {
        var username = _config.MyFitnessPalUsername;
        if (string.IsNullOrWhiteSpace(username))
        {
            _logger.LogWarning("Cannot fetch MyFitnessPal data - Username not configured");
            return Enumerable.Empty<ConnectorFoodEntryImport>();
        }

        var diaryResponse = await FetchDiaryAsync(username, from, to);
        return ConvertToConnectorFoodEntryImports(diaryResponse);
    }

    private IEnumerable<ConnectorFoodEntryImport> ConvertToConnectorFoodEntryImports(
        DiaryResponse diaryResponse
    )
    {
        var imports = new List<ConnectorFoodEntryImport>();

        foreach (var diaryEntry in diaryResponse)
        {
            var diaryDate = ParseDiaryDate(diaryEntry.Date);

            foreach (var foodEntry in diaryEntry.FoodEntries)
            {
                var consumedAt =
                    ParseDateTimeOffset(foodEntry.ConsumedAt)
                    ?? ParseDateTimeOffset(foodEntry.LoggedAt)
                    ?? diaryDate
                    ?? DateTimeOffset.UtcNow;

                var loggedAt = ParseDateTimeOffset(foodEntry.LoggedAt);

                var servingDescription = foodEntry.ServingSize.Value > 0
                    ? $"{foodEntry.ServingSize.Value} {foodEntry.ServingSize.Unit}"
                    : null;

                var import = new ConnectorFoodEntryImport
                {
                    ConnectorSource = ConnectorSource,
                    ExternalEntryId = foodEntry.Id,
                    ExternalFoodId = foodEntry.Food.Id,
                    ConsumedAt = consumedAt,
                    LoggedAt = loggedAt,
                    MealName = foodEntry.MealName,
                    Carbs = (decimal)(foodEntry.NutritionalContents.Carbohydrates ?? 0),
                    Protein = (decimal)(foodEntry.NutritionalContents.Protein ?? 0),
                    Fat = (decimal)(foodEntry.NutritionalContents.Fat ?? 0),
                    Energy = (decimal)(foodEntry.NutritionalContents.Energy?.Value ?? 0),
                    Servings = (decimal)foodEntry.Servings,
                    ServingDescription = servingDescription,
                    Food = BuildFoodImport(foodEntry),
                };

                imports.Add(import);
            }
        }

        return imports;
    }

    private static ConnectorFoodImport BuildFoodImport(FoodEntry foodEntry)
    {
        var food = foodEntry.Food;
        var portion = foodEntry.ServingSize.Value > 0 ? foodEntry.ServingSize.Value : 1;

        return new ConnectorFoodImport
        {
            ExternalId = food.Id,
            Name = food.Description,
            BrandName = string.IsNullOrWhiteSpace(food.BrandName) ? null : food.BrandName,
            Carbs = (decimal)(food.NutritionalContents.Carbohydrates ?? 0),
            Protein = (decimal)(food.NutritionalContents.Protein ?? 0),
            Fat = (decimal)(food.NutritionalContents.Fat ?? 0),
            Energy = (decimal)(food.NutritionalContents.Energy?.Value ?? 0),
            Portion = (decimal)portion,
            Unit = foodEntry.ServingSize.Unit,
        };
    }

    private static DateTimeOffset? ParseDateTimeOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed
        )
            ? parsed
            : null;
    }

    private static DateTimeOffset? ParseDiaryDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed
        )
            ? new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Utc))
            : null;
    }

    /// <summary>
    /// Initializes a new instance of the MyFitnessPalConnectorService
    /// </summary>
    /// <param name="config">Connector configuration</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="httpClient">HTTP client instance</param>
    /// <param name="createFoodAsync">Optional delegate for creating food records directly</param>
    public MyFitnessPalConnectorService(
        HttpClient httpClient,
        IOptions<MyFitnessPalConnectorConfiguration> config,
        ILogger<MyFitnessPalConnectorService> logger,
        IApiDataSubmitter? apiDataSubmitter = null,
        IConnectorMetricsTracker? metricsTracker = null,
        Func<IEnumerable<Food>, CancellationToken, Task<IEnumerable<Food>>>? createFoodAsync = null,
        IConnectorStateService? stateService = null
    )
        : base(httpClient, logger, apiDataSubmitter, metricsTracker, stateService)
    {
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _createFoodAsync = createFoodAsync;
    }

    /// <summary>
    /// Authenticates with MyFitnessPal (not required for public diary access)
    /// </summary>
    /// <returns>Always returns true as no authentication is required</returns>
    public override async Task<bool> AuthenticateAsync()
    {
        // MyFitnessPal diary API doesn't require authentication for public diaries
        _logger.LogInformation("MyFitnessPal connector authenticated (no auth required)");
        return await Task.FromResult(true);
    }

    /// <summary>
    /// Fetches diary data from MyFitnessPal for a specific username and date range.
    /// Uses curl to bypass Cloudflare TLS fingerprinting.
    /// </summary>
    /// <param name="username">MyFitnessPal username</param>
    /// <param name="fromDate">Start date for diary data (optional, defaults to today)</param>
    /// <param name="toDate">End date for diary data (optional, defaults to fromDate)</param>
    /// <returns>Diary response containing food entries</returns>
    public async Task<DiaryResponse> FetchDiaryAsync(
        string username,
        DateTime? fromDate = null,
        DateTime? toDate = null
    )
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be null or empty", nameof(username));
        }

        var from = fromDate ?? DateTime.Today;
        var to = toDate ?? from;

        // MyFitnessPal uses YYYY-MM-DD format
        var fromFormatted = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var toFormatted = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        _logger.LogInformation(
            "Fetching MyFitnessPal diary for user {Username} from {FromDate} to {ToDate}",
            username,
            fromFormatted,
            toFormatted
        );

        // Use API key if configured, otherwise empty string for public diaries
        var apiKey = _config.MyFitnessPalApiKey ?? "";

        var requestBody = new
        {
            key = apiKey,
            username,
            from = fromFormatted,
            to = toFormatted,
            show_food_diary = 1,
            show_food_notes = 0,
            show_exercise_diary = 1,
            show_exercise_notes = 0,
        };

        var jsonBody = JsonSerializer.Serialize(requestBody);
        var requestUrl = $"{MyFitnessPalApiUrlBase}?username={Uri.EscapeDataString(username)}";

        try
        {
            // Use curl to bypass Cloudflare TLS fingerprinting
            var responseContent = await ExecuteCurlRequestAsync(requestUrl, jsonBody);
            var diaryResponse = JsonSerializer.Deserialize<DiaryResponse>(responseContent);

            if (diaryResponse == null)
            {
                throw new InvalidOperationException(
                    "Failed to deserialize MyFitnessPal diary response"
                );
            }

            _logger.LogInformation(
                "[{ConnectorSource}] Successfully fetched MyFitnessPal diary data with {EntryCount} diary entries",
                ConnectorSource,
                diaryResponse.Count
            );

            return diaryResponse;
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "JSON deserialization error while processing MyFitnessPal diary response"
            );
            throw new InvalidOperationException(
                $"Failed to parse MyFitnessPal diary response: {ex.Message}",
                ex
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while fetching MyFitnessPal diary for user {Username}",
                username
            );
            throw;
        }
    }

    /// <summary>
    /// Executes an HTTP POST request using curl to bypass Cloudflare TLS fingerprinting
    /// </summary>
    private async Task<string> ExecuteCurlRequestAsync(string url, string jsonBody)
    {
        // Escape the JSON body for command line - escape double quotes
        var escapedJson = jsonBody.Replace("\"", "\\\"");

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "curl",
            Arguments = $"-s -X POST \"{url}\" -H \"Content-Type: application/json\" -H \"User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36\" -d \"{escapedJson}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        _logger.LogDebug("Executing curl request to {Url}", url);

        using var process = new System.Diagnostics.Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            _logger.LogError("curl failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
            throw new InvalidOperationException($"curl request failed: {error}");
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException("curl returned empty response");
        }

        return output;
    }

    /// <summary>
    /// Converts MyFitnessPal food entries to Nightscout food entries
    /// </summary>
    /// <param name="diaryResponse">MyFitnessPal diary response</param>
    /// <returns>Collection of Nightscout food entries</returns>
    public IEnumerable<Food> ConvertToNightscoutFoods(DiaryResponse diaryResponse)
    {
        var foods = new List<Food>();

        foreach (var diaryEntry in diaryResponse)
        {
            foreach (var foodEntry in diaryEntry.FoodEntries)
            {
                var food = new Food
                {
                    Type = "food",
                    Name = foodEntry.MealName,
                    Portion = foodEntry.Servings,
                    Unit = foodEntry.ServingSize.Unit,
                    Carbs = foodEntry.NutritionalContents.Carbohydrates ?? 0,
                    Protein = foodEntry.NutritionalContents.Protein ?? 0,
                    Fat = foodEntry.NutritionalContents.Fat ?? 0,
                    Energy = (int)(foodEntry.NutritionalContents.Energy?.Value ?? 0),
                };

                foods.Add(food);
            }
        }

        return foods;
    }

    /// <summary>
    /// Fetches glucose data (not applicable for MyFitnessPal)
    /// </summary>
    /// <param name="since">Since timestamp (ignored)</param>
    /// <returns>Empty collection as MyFitnessPal doesn't provide glucose data</returns>
    public override async Task<IEnumerable<Entry>> FetchGlucoseDataAsync(DateTime? since = null)
    {
        _logger.LogWarning(
            "FetchGlucoseDataAsync called on MyFitnessPal connector - MyFitnessPal doesn't provide glucose data"
        );
        return await Task.FromResult(Enumerable.Empty<Entry>());
    }


    /// <summary>
    /// Compute SHA1 hash for API secret authentication
    /// </summary>
    /// <param name="input">Input string to hash</param>
    /// <returns>SHA1 hash as lowercase hex string</returns>
    private static string ComputeSha1Hash(string input)
    {
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha1.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Disposes the connector service
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // HttpClient is managed by the base class
        }
        base.Dispose(disposing);
    }

}
