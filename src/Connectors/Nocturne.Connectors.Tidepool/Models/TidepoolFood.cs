using System.Text.Json.Serialization;

namespace Nocturne.Connectors.Tidepool.Models;

/// <summary>
/// Food/carbohydrate entry from Tidepool
/// </summary>
public class TidepoolFood
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("time")]
    public DateTime? Time { get; set; }

    [JsonPropertyName("deviceTime")]
    public DateTime? DeviceTime { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "food";

    [JsonPropertyName("uploadId")]
    public string? UploadId { get; set; }

    [JsonPropertyName("nutrition")]
    public TidepoolNutrition? Nutrition { get; set; }
}

/// <summary>
/// Nutrition information
/// </summary>
public class TidepoolNutrition
{
    [JsonPropertyName("carbohydrate")]
    public TidepoolCarbohydrate? Carbohydrate { get; set; }
}

/// <summary>
/// Carbohydrate information
/// </summary>
public class TidepoolCarbohydrate
{
    [JsonPropertyName("net")]
    public double? Net { get; set; }

    [JsonPropertyName("units")]
    public string Units { get; set; } = "grams";
}
