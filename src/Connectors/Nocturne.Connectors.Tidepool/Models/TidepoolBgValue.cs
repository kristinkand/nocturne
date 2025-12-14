using System.Text.Json.Serialization;

namespace Nocturne.Connectors.Tidepool.Models;

/// <summary>
/// Blood glucose value from Tidepool (cbg or smbg type)
/// </summary>
public class TidepoolBgValue
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("time")]
    public DateTime? Time { get; set; }

    [JsonPropertyName("deviceTime")]
    public DateTime? DeviceTime { get; set; }

    [JsonPropertyName("deviceId")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("units")]
    public string Units { get; set; } = "mg/dL";

    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("uploadId")]
    public string? UploadId { get; set; }

    [JsonPropertyName("clockDriftOffset")]
    public int? ClockDriftOffset { get; set; }

    [JsonPropertyName("conversionOffset")]
    public int? ConversionOffset { get; set; }

    [JsonPropertyName("guid")]
    public string? Guid { get; set; }
}
