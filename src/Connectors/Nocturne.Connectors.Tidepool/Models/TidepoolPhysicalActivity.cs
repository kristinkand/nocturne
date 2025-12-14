using System.Text.Json.Serialization;

namespace Nocturne.Connectors.Tidepool.Models;

/// <summary>
/// Physical activity/exercise from Tidepool
/// </summary>
public class TidepoolPhysicalActivity
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("time")]
    public DateTime? Time { get; set; }

    [JsonPropertyName("deviceTime")]
    public DateTime? DeviceTime { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "physicalActivity";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("uploadId")]
    public string? UploadId { get; set; }

    [JsonPropertyName("distance")]
    public TidepoolMeasurement? Distance { get; set; }

    [JsonPropertyName("duration")]
    public TidepoolMeasurement? Duration { get; set; }

    [JsonPropertyName("energy")]
    public TidepoolMeasurement? Energy { get; set; }
}

/// <summary>
/// Generic measurement with value and units
/// </summary>
public class TidepoolMeasurement
{
    [JsonPropertyName("value")]
    public double? Value { get; set; }

    [JsonPropertyName("units")]
    public string? Units { get; set; }
}
