using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.Core.Services;
using Nocturne.Connectors.MyFitnessPal.Models;
using Nocturne.Core.Models;

namespace Nocturne.Connectors.MyFitnessPal.Services;

/// <summary>
/// Connector service for MyFitnessPal food diary data
/// Supports both direct integration via dependency injection and HTTP fallback
/// </summary>
public class MyFitnessPalConnectorService : BaseConnectorService<MyFitnessPalConnectorConfiguration>
{
    private readonly MyFitnessPalConnectorConfiguration _config;
    private new readonly ILogger<MyFitnessPalConnectorService> _logger;
    private readonly Func<
        IEnumerable<Food>,
        CancellationToken,
        Task<IEnumerable<Food>>
    >? _createFoodAsync;
    private const string MyFitnessPalApiUrl =
        "https://www.myfitnesspal.com/api/services/authenticate_diary_key";

    /// <summary>
    /// Gets the connector source identifier
    /// </summary>
    public override string ConnectorSource => "myfitnesspal";

    /// <summary>
    /// Gets the service name for this connector
    /// </summary>
    public override string ServiceName => "MyFitnessPal";

    /// <summary>
    /// Initializes a new instance of the MyFitnessPalConnectorService
    /// </summary>
    /// <param name="config">Connector configuration</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="httpClient">HTTP client instance</param>
    /// <param name="createFoodAsync">Optional delegate for creating food records directly</param>
    public MyFitnessPalConnectorService(
        MyFitnessPalConnectorConfiguration config,
        ILogger<MyFitnessPalConnectorService> logger,
        HttpClient httpClient,
        Func<IEnumerable<Food>, CancellationToken, Task<IEnumerable<Food>>>? createFoodAsync = null
    )
        : base(httpClient)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
    /// Fetches diary data from MyFitnessPal for a specific username and date range
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

        var requestBody = new
        {
            username,
            from = fromFormatted,
            to = toFormatted,
            show_food_diary = 1,
            show_food_notes = 1,
            show_exercise_diary = 0,
            show_exercise_notes = 0,
        };

        var jsonBody = JsonSerializer.Serialize(requestBody);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, MyFitnessPalApiUrl)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
            };

            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var diaryResponse = JsonSerializer.Deserialize<DiaryResponse>(responseContent);

            if (diaryResponse == null)
            {
                throw new InvalidOperationException(
                    "Failed to deserialize MyFitnessPal diary response"
                );
            }

            _logger.LogInformation(
                "Successfully fetched MyFitnessPal diary data with {EntryCount} diary entries",
                diaryResponse.Count
            );

            return diaryResponse;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "HTTP error while fetching MyFitnessPal diary for user {Username}",
                username
            );
            throw new InvalidOperationException(
                $"Failed to fetch MyFitnessPal diary: {ex.Message}",
                ex
            );
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
    /// Uploads food data using the injected food service or fallback to HTTP
    /// </summary>
    /// <param name="foods">Food entries to upload</param>
    /// <param name="config">Connector configuration (used for HTTP fallback only)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if upload succeeded</returns>
    public async Task<bool> UploadFoodToNightscoutAsync(
        IEnumerable<Food> foods,
        MyFitnessPalConnectorConfiguration config,
        CancellationToken cancellationToken = default
    )
    {
        if (!foods.Any())
        {
            _logger.LogInformation("No food entries to upload to Nightscout");
            return true;
        }

        try
        {
            var foodList = foods.ToList();
            _logger.LogInformation(
                "Uploading {FoodCount} food entries to Nightscout",
                foodList.Count
            );

            bool success;

            // Use injected food service if available
            if (_createFoodAsync != null)
            {
                _logger.LogDebug("Using direct food service for upload");
                var createdFoods = await _createFoodAsync(foodList, cancellationToken);
                success = createdFoods.Any();

                if (success)
                {
                    _logger.LogInformation(
                        "Successfully uploaded {FoodCount} food entries via direct service",
                        createdFoods.Count()
                    );
                }
            }
            else
            {
                _logger.LogDebug("Using HTTP API for upload (fallback)");
                // Fallback to HTTP upload using the food API endpoint
                success = await UploadFoodBatchToNightscoutAsync(
                    foodList,
                    config.NightscoutUrl,
                    config.NightscoutApiSecret ?? string.Empty
                );

                if (success)
                {
                    _logger.LogInformation(
                        "Successfully uploaded {FoodCount} food entries via HTTP API",
                        foodList.Count
                    );
                }
            }

            if (!success)
            {
                _logger.LogError(
                    "Failed to upload {FoodCount} food entries to Nightscout",
                    foodList.Count
                );
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload food entries to Nightscout");
            return false;
        }
    }

    /// <summary>
    /// Uploads food data using the injected food service or fallback to HTTP (backward compatible overload)
    /// </summary>
    /// <param name="foods">Food entries to upload</param>
    /// <param name="config">Connector configuration</param>
    /// <returns>True if upload succeeded</returns>
    public async Task<bool> UploadFoodToNightscoutAsync(
        IEnumerable<Food> foods,
        MyFitnessPalConnectorConfiguration config
    )
    {
        return await UploadFoodToNightscoutAsync(foods, config, CancellationToken.None);
    }

    /// <summary>
    /// Upload a batch of food entries to Nightscout food API
    /// </summary>
    /// <param name="foods">Food entries to upload</param>
    /// <param name="nightscoutUrl">Nightscout base URL</param>
    /// <param name="apiSecret">API secret for authentication</param>
    /// <returns>True if upload succeeded</returns>
    private async Task<bool> UploadFoodBatchToNightscoutAsync(
        List<Food> foods,
        string nightscoutUrl,
        string apiSecret
    )
    {
        if (string.IsNullOrWhiteSpace(nightscoutUrl))
        {
            _logger.LogError("Nightscout URL is not configured");
            return false;
        }

        try
        {
            var baseUri = new Uri(nightscoutUrl.TrimEnd('/'));
            var foodApiUri = new Uri(baseUri, "/api/v1/food");

            // Use the injected HttpClient instead of creating a new one
            var originalTimeout = _httpClient.Timeout;
            _httpClient.Timeout = TimeSpan.FromMinutes(5);

            try
            {
                // Add API secret authentication header if provided
                if (!string.IsNullOrWhiteSpace(apiSecret))
                {
                    var hash = ComputeSha1Hash(apiSecret);
                    _httpClient.DefaultRequestHeaders.Remove("api-secret"); // Remove any existing header
                    _httpClient.DefaultRequestHeaders.Add("api-secret", hash);
                }

                // Convert foods to JSON
                var json = JsonSerializer.Serialize(foods);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogDebug(
                    "Uploading {Count} food entries to {Url}",
                    foods.Count,
                    foodApiUri
                );

                var response = await _httpClient.PostAsync(foodApiUri, content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Successfully uploaded food batch to Nightscout");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError(
                        "Failed to upload food batch. Status: {StatusCode}, Response: {Response}",
                        response.StatusCode,
                        errorContent
                    );
                    return false;
                }
            }
            finally
            {
                // Restore original timeout and clean up headers
                _httpClient.Timeout = originalTimeout;
                _httpClient.DefaultRequestHeaders.Remove("api-secret");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error uploading food batch to Nightscout");
            return false;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout uploading food batch to Nightscout");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading food batch to Nightscout");
            return false;
        }
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

    /// <summary>
    /// Fetches MyFitnessPal diary data, converts to Nightscout foods, and uploads them
    /// </summary>
    /// <param name="username">MyFitnessPal username</param>
    /// <param name="fromDate">Start date for diary data (optional, defaults to today)</param>
    /// <param name="toDate">End date for diary data (optional, defaults to fromDate)</param>
    /// <param name="config">Connector configuration for HTTP fallback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful</returns>
    public async Task<bool> FetchAndUploadFoodsAsync(
        string username,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        MyFitnessPalConnectorConfiguration? config = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogInformation("Starting MyFitnessPal food sync for user {Username}", username);

            // Fetch diary data from MyFitnessPal
            var diaryResponse = await FetchDiaryAsync(username, fromDate, toDate);

            // Convert to Nightscout foods
            var foods = ConvertToNightscoutFoods(diaryResponse);
            var foodList = foods.ToList();

            if (!foodList.Any())
            {
                _logger.LogInformation("No food entries found to sync");
                return true;
            }

            // Upload foods
            var success = await UploadFoodToNightscoutAsync(
                foodList,
                config ?? _config,
                cancellationToken
            );

            if (success)
            {
                _logger.LogInformation(
                    "Successfully synced {FoodCount} food entries from MyFitnessPal",
                    foodList.Count
                );
            }
            else
            {
                _logger.LogError("Failed to sync food entries from MyFitnessPal");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error during MyFitnessPal food sync for user {Username}",
                username
            );
            return false;
        }
    }
}
