using System.Text.Json.Serialization;

namespace Nocturne.Core.Models;

/// <summary>
/// Represents a Nightscout profile record for the API
/// Compatible with the legacy Nightscout profiles collection
/// </summary>
public class Profile
{
    /// <summary>
    /// Gets or sets the MongoDB ObjectId
    /// </summary>
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the default profile in the store
    /// </summary>
    [JsonPropertyName("defaultProfile")]
    public string DefaultProfile { get; set; } = "Default";

    /// <summary>
    /// Gets or sets the start date for this profile record
    /// </summary>
    [JsonPropertyName("startDate")]
    public string StartDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    /// <summary>
    /// Gets or sets the time in milliseconds since the Unix epoch
    /// </summary>
    [JsonPropertyName("mills")]
    public long Mills { get; set; }

    /// <summary>
    /// Gets or sets when this profile was created
    /// </summary>
    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the units used for blood glucose values (mg/dL or mmol/L)
    /// </summary>
    [JsonPropertyName("units")]
    public string Units { get; set; } = "mg/dL";

    /// <summary>
    /// Gets or sets the store containing all named profiles
    /// </summary>
    [JsonPropertyName("store")]
    public Dictionary<string, ProfileData> Store { get; set; } = new();

    /// <summary>
    /// Gets or sets whether this profile was converted on the fly from legacy format
    /// </summary>
    [JsonIgnore]
    public bool ConvertedOnTheFly { get; set; }
}

/// <summary>
/// Represents the data for a specific named profile within a profile record
/// </summary>
public class ProfileData
{
    /// <summary>
    /// Gets or sets the duration of insulin action in hours
    /// </summary>
    [JsonPropertyName("dia")]
    public double Dia { get; set; } = 3.0;

    /// <summary>
    /// Gets or sets the carbs absorption rate in grams per hour
    /// </summary>
    [JsonPropertyName("carbs_hr")]
    public int CarbsHr { get; set; } = 20;

    /// <summary>
    /// Gets or sets the carb absorption delay in minutes
    /// </summary>
    [JsonPropertyName("delay")]
    public int Delay { get; set; } = 20;

    /// <summary>
    /// Gets or sets the timezone for this profile
    /// </summary>
    [JsonPropertyName("timezone")]
    public string? Timezone { get; set; }

    /// <summary>
    /// Gets or sets the units used for blood glucose values
    /// </summary>
    [JsonPropertyName("units")]
    public string? Units { get; set; }

    /// <summary>
    /// Gets or sets whether to use GI-specific carb values
    /// </summary>
    [JsonPropertyName("perGIvalues")]
    public bool? PerGIValues { get; set; }

    /// <summary>
    /// Gets or sets the carbs absorption rate for high GI foods
    /// </summary>
    [JsonPropertyName("carbs_hr_high")]
    public int? CarbsHrHigh { get; set; }

    /// <summary>
    /// Gets or sets the carbs absorption rate for medium GI foods
    /// </summary>
    [JsonPropertyName("carbs_hr_medium")]
    public int? CarbsHrMedium { get; set; }

    /// <summary>
    /// Gets or sets the carbs absorption rate for low GI foods
    /// </summary>
    [JsonPropertyName("carbs_hr_low")]
    public int? CarbsHrLow { get; set; }

    /// <summary>
    /// Gets or sets the delay for high GI carbs
    /// </summary>
    [JsonPropertyName("delay_high")]
    public int? DelayHigh { get; set; }

    /// <summary>
    /// Gets or sets the delay for medium GI carbs
    /// </summary>
    [JsonPropertyName("delay_medium")]
    public int? DelayMedium { get; set; }

    /// <summary>
    /// Gets or sets the delay for low GI carbs
    /// </summary>
    [JsonPropertyName("delay_low")]
    public int? DelayLow { get; set; }

    /// <summary>
    /// Gets or sets the basal rates throughout the day
    /// </summary>
    [JsonPropertyName("basal")]
    public List<TimeValue> Basal { get; set; } = new();

    /// <summary>
    /// Gets or sets the carb ratios throughout the day
    /// </summary>
    [JsonPropertyName("carbratio")]
    public List<TimeValue> CarbRatio { get; set; } = new();

    /// <summary>
    /// Gets or sets the insulin sensitivity factors throughout the day
    /// </summary>
    [JsonPropertyName("sens")]
    public List<TimeValue> Sens { get; set; } = new();

    /// <summary>
    /// Gets or sets the low blood glucose targets throughout the day
    /// </summary>
    [JsonPropertyName("target_low")]
    public List<TimeValue> TargetLow { get; set; } = new();

    /// <summary>
    /// Gets or sets the high blood glucose targets throughout the day
    /// </summary>
    [JsonPropertyName("target_high")]
    public List<TimeValue> TargetHigh { get; set; } = new();

    /// <summary>
    /// Gets or sets whether this profile was converted on the fly from legacy format
    /// </summary>
    [JsonIgnore]
    public bool ConvertedOnTheFly { get; set; }
}

/// <summary>
/// Represents a time-based value used in profiles (e.g., basal rates, carb ratios)
/// </summary>
public class TimeValue
{
    /// <summary>
    /// Gets or sets the time in HH:mm format (e.g., "06:00")
    /// </summary>
    [JsonPropertyName("time")]
    public string Time { get; set; } = "00:00";

    /// <summary>
    /// Gets or sets the value for this time period
    /// </summary>
    [JsonPropertyName("value")]
    public double Value { get; set; }

    /// <summary>
    /// Gets or sets the time converted to seconds since midnight for faster calculations
    /// This property is calculated at load time and not persisted
    /// </summary>
    [JsonIgnore]
    public int? TimeAsSeconds { get; set; }
}
