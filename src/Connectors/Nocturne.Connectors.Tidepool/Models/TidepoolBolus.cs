using System.Text.Json.Serialization;

namespace Nocturne.Connectors.Tidepool.Models;

/// <summary>
/// Bolus delivery from Tidepool
/// </summary>
public class TidepoolBolus
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("time")]
    public DateTime? Time { get; set; }

    [JsonPropertyName("deviceTime")]
    public DateTime? DeviceTime { get; set; }

    [JsonPropertyName("deviceId")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "bolus";

    /// <summary>
    /// Normal (immediate) bolus amount in units
    /// </summary>
    [JsonPropertyName("normal")]
    public double? Normal { get; set; }

    /// <summary>
    /// Extended bolus amount in units
    /// </summary>
    [JsonPropertyName("extended")]
    public double? Extended { get; set; }

    /// <summary>
    /// Duration in milliseconds for extended/combo bolus
    /// </summary>
    [JsonPropertyName("duration")]
    public long? DurationMs { get; set; }

    /// <summary>
    /// Bolus sub-type (normal, square, dual/square)
    /// </summary>
    [JsonPropertyName("subType")]
    public string? SubType { get; set; }

    [JsonPropertyName("uploadId")]
    public string? UploadId { get; set; }

    /// <summary>
    /// Duration as TimeSpan
    /// </summary>
    public TimeSpan? Duration => DurationMs.HasValue
        ? TimeSpan.FromMilliseconds(DurationMs.Value)
        : null;

    /// <summary>
    /// Total insulin delivered (normal + extended)
    /// </summary>
    public double TotalInsulin => (Normal ?? 0) + (Extended ?? 0);
}
