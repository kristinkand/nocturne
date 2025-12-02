using System.Text.Json.Serialization;
using Nocturne.Core.Models.Serializers;

namespace Nocturne.Core.Models;

/// <summary>
/// Represents a Nightscout treatment entry with 1:1 legacy JavaScript compatibility
/// Compatible with both the API and Connect projects
/// </summary>
public class Treatment : ProcessableDocumentBase
{
    /// <summary>
    /// Gets or sets the MongoDB ObjectId
    /// </summary>
    [JsonPropertyName("_id")]
    public override string? Id { get; set; }

    /// <summary>
    /// Gets or sets the event type (e.g., "Meal Bolus", "Correction Bolus", "BG Check")
    /// </summary>
    [JsonPropertyName("eventType")]
    [Sanitizable]
    public string? EventType { get; set; }

    /// <summary>
    /// Gets or sets the treatment reason
    /// </summary>
    [JsonPropertyName("reason")]
    [Sanitizable]
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the glucose value for the treatment
    /// </summary>
    [JsonPropertyName("glucose")]
    public double? Glucose { get; set; }

    /// <summary>
    /// Gets or sets the glucose type (e.g., "Finger", "Sensor")
    /// </summary>
    [JsonPropertyName("glucoseType")]
    public string? GlucoseType { get; set; }

    /// <summary>
    /// Gets or sets the carbohydrates in grams
    /// </summary>
    [JsonPropertyName("carbs")]
    public double? Carbs { get; set; }

    /// <summary>
    /// Gets or sets the insulin amount in units
    /// </summary>
    [JsonPropertyName("insulin")]
    public double? Insulin { get; set; }

    /// <summary>
    /// Gets or sets the protein content in grams
    /// </summary>
    [JsonPropertyName("protein")]
    public double? Protein { get; set; }

    /// <summary>
    /// Gets or sets the fat content in grams
    /// </summary>
    [JsonPropertyName("fat")]
    public double? Fat { get; set; }

    /// <summary>
    /// Gets or sets the food type
    /// </summary>
    [JsonPropertyName("foodType")]
    [Sanitizable]
    public string? FoodType { get; set; }

    /// <summary>
    /// Gets or sets the units (e.g., "mg/dl", "mmol")
    /// </summary>
    [JsonPropertyName("units")]
    public string? Units { get; set; }

    /// <summary>
    /// Gets or sets the time in milliseconds since the Unix epoch
    /// </summary>
    [JsonPropertyName("mills")]
    public override long Mills
    {
        get
        {
            if (_mills == 0 && !string.IsNullOrEmpty(_created_at))
            {
                if (
                    DateTime.TryParse(
                        _created_at,
                        null,
                        System.Globalization.DateTimeStyles.RoundtripKind,
                        out var parsedDate
                    )
                )
                {
                    return (
                        (DateTimeOffset)DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc)
                    ).ToUnixTimeMilliseconds();
                }
            }
            return _mills;
        }
        set => _mills = value;
    }
    private long _mills;

    /// <summary>
    /// Gets or sets the created at timestamp as ISO string
    /// </summary>
    [JsonPropertyName("created_at")]
    public string? Created_at
    {
        get
        {
            if (string.IsNullOrEmpty(_created_at) && _mills > 0)
            {
                return DateTimeOffset
                    .FromUnixTimeMilliseconds(_mills)
                    .ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            }
            return _created_at;
        }
        set => _created_at = value;
    }
    private string? _created_at;

    /// <summary>
    /// Gets or sets the treatment duration in minutes
    /// </summary>
    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    /// <summary>
    /// Gets or sets the percent of temporary basal rate
    /// </summary>
    [JsonPropertyName("percent")]
    public double? Percent { get; set; }

    /// <summary>
    /// Gets or sets the absolute temporary basal rate
    /// </summary>
    [JsonPropertyName("absolute")]
    public double? Absolute { get; set; }

    /// <summary>
    /// Gets or sets the treatment notes
    /// </summary>
    [JsonPropertyName("notes")]
    [Sanitizable]
    public string? Notes { get; set; }

    /// <summary>
    /// Gets or sets who entered the treatment
    /// </summary>
    [JsonPropertyName("enteredBy")]
    [Sanitizable]
    public string? EnteredBy { get; set; }

    /// <summary>
    /// Gets or sets the treatment target top
    /// </summary>
    [JsonPropertyName("targetTop")]
    public double? TargetTop { get; set; }

    /// <summary>
    /// Gets or sets the treatment target bottom
    /// </summary>
    [JsonPropertyName("targetBottom")]
    public double? TargetBottom { get; set; }

    /// <summary>
    /// Gets or sets the treatment profile
    /// </summary>
    [JsonPropertyName("profile")]
    public string? Profile { get; set; }

    /// <summary>
    /// Gets or sets whether this entry was split from another
    /// </summary>
    [JsonPropertyName("split")]
    public string? Split { get; set; }

    /// <summary>
    /// Gets or sets when this treatment was created
    /// </summary>
    [JsonPropertyName("date")]
    public long? Date { get; set; }

    /// <summary>
    /// Gets or sets the carb time offset
    /// </summary>
    [JsonPropertyName("carbTime")]
    public int? CarbTime { get; set; }

    /// <summary>
    /// Gets or sets the bolus calculator values
    /// </summary>
    [JsonPropertyName("boluscalc")]
    public Dictionary<string, object>? BolusCalc { get; set; }

    /// <summary>
    /// Gets or sets the UTCOFFSET
    /// </summary>
    [JsonPropertyName("utcOffset")]
    public override int? UtcOffset { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp - alias for Created_at for API compatibility
    /// </summary>
    [JsonIgnore]
    public override string? CreatedAt
    {
        get => Created_at;
        set => Created_at = value;
    }

    /// <summary>
    /// Gets or sets the timestamp in milliseconds since Unix epoch - optional field
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; set; }

    /// <summary>
    /// Calculates Mills from Created_at if Mills is not set - for API compatibility
    /// </summary>
    [JsonIgnore]
    public long CalculatedMills
    {
        get
        {
            if (Mills > 0)
                return Mills;

            if (
                !string.IsNullOrEmpty(Created_at)
                && DateTime.TryParse(Created_at, out var createdAtDate)
            )
                return ((DateTimeOffset)createdAtDate).ToUnixTimeMilliseconds();

            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    /// <summary>
    /// Gets or sets the profile name that cut this treatment (used by duration processing)
    /// </summary>
    [JsonPropertyName("cuttedby")]
    public string? CuttedBy { get; set; }

    /// <summary>
    /// Gets or sets the profile name that this treatment cut (used by duration processing)
    /// </summary>
    [JsonPropertyName("cutting")]
    public string? Cutting { get; set; }

    /// <summary>
    /// Gets or sets the source of the treatment data
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets the event time as ISO string (used by Glooko connector)
    /// </summary>
    [JsonPropertyName("eventTime")]
    public string? EventTime { get; set; }

    /// <summary>
    /// Gets or sets the pre-bolus time in minutes (used by Glooko connector)
    /// </summary>
    [JsonPropertyName("preBolus")]
    public double? PreBolus { get; set; }

    /// <summary>
    /// Gets or sets the basal rate (used for temp basal treatments)
    /// </summary>
    [JsonPropertyName("rate")]
    public double? Rate { get; set; }

    /// <summary>
    /// Gets or sets the blood glucose value in mg/dL
    /// </summary>
    [JsonPropertyName("mgdl")]
    public double? Mgdl { get; set; }

    /// <summary>
    /// Gets or sets the blood glucose value in mmol/L
    /// </summary>
    [JsonPropertyName("mmol")]
    public double? Mmol { get; set; }

    /// <summary>
    /// Gets or sets the end time in milliseconds for duration treatments
    /// </summary>
    [JsonPropertyName("endmills")]
    public long? EndMills { get; set; }

    /// <summary>
    /// Gets or sets the duration type (e.g., "indefinite")
    /// </summary>
    [JsonPropertyName("durationType")]
    [Sanitizable]
    public string? DurationType { get; set; }

    /// <summary>
    /// Gets or sets whether this treatment is an announcement
    /// </summary>
    [JsonPropertyName("isAnnouncement")]
    [JsonConverter(typeof(FlexibleBooleanJsonConverter))]
    public bool? IsAnnouncement { get; set; }

    /// <summary>
    /// Gets or sets the JSON string of profile data for profile switches
    /// </summary>
    [JsonPropertyName("profileJson")]
    [Sanitizable]
    public string? ProfileJson { get; set; }

    /// <summary>
    /// Gets or sets the end profile name for profile switches
    /// </summary>
    [JsonPropertyName("endprofile")]
    [Sanitizable]
    public string? EndProfile { get; set; }

    /// <summary>
    /// Gets or sets the insulin scaling factor for adjustments
    /// </summary>
    [JsonPropertyName("insulinNeedsScaleFactor")]
    public double? InsulinNeedsScaleFactor { get; set; }

    /// <summary>
    /// Gets or sets the carb absorption time in minutes
    /// </summary>
    [JsonPropertyName("absorptionTime")]
    public int? AbsorptionTime { get; set; }

    /// <summary>
    /// Gets or sets the manually entered insulin amount (for combo bolus)
    /// </summary>
    [JsonPropertyName("enteredinsulin")]
    public double? EnteredInsulin { get; set; }

    /// <summary>
    /// Gets or sets the percentage of combo bolus delivered immediately
    /// </summary>
    [JsonPropertyName("splitNow")]
    public double? SplitNow { get; set; }

    /// <summary>
    /// Gets or sets the percentage of combo bolus delivered extended
    /// </summary>
    [JsonPropertyName("splitExt")]
    public double? SplitExt { get; set; }

    /// <summary>
    /// Gets or sets the treatment status
    /// </summary>
    [JsonPropertyName("status")]
    [Sanitizable]
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets the relative basal rate change
    /// </summary>
    [JsonPropertyName("relative")]
    public double? Relative { get; set; }

    /// <summary>
    /// Gets or sets the carb ratio
    /// </summary>
    [JsonPropertyName("CR")]
    public double? CR { get; set; }

    /// <summary>
    /// Gets or sets the Nightscout client identifier
    /// </summary>
    [JsonPropertyName("NSCLIENT_ID")]
    [Sanitizable]
    public string? NsClientId { get; set; }

    /// <summary>
    /// Gets or sets whether this is the first treatment in a series
    /// </summary>
    [JsonPropertyName("first")]
    [JsonConverter(typeof(FlexibleBooleanJsonConverter))]
    public bool? First { get; set; }

    /// <summary>
    /// Gets or sets whether this is the end treatment in a series
    /// </summary>
    [JsonPropertyName("end")]
    [JsonConverter(typeof(FlexibleBooleanJsonConverter))]
    public bool? End { get; set; }

    /// <summary>
    /// Gets or sets whether this is a CircadianPercentageProfile treatment
    /// </summary>
    [JsonPropertyName("CircadianPercentageProfile")]
    [JsonConverter(typeof(FlexibleBooleanJsonConverter))]
    public bool? CircadianPercentageProfile { get; set; }

    /// <summary>
    /// Gets or sets the percentage for CircadianPercentageProfile
    /// </summary>
    [JsonPropertyName("percentage")]
    public double? Percentage { get; set; }

    /// <summary>
    /// Gets or sets the timeshift for CircadianPercentageProfile (in hours)
    /// </summary>
    [JsonPropertyName("timeshift")]
    public double? Timeshift { get; set; }

    /// <summary>
    /// Gets or sets the transmitter ID (used by CGM devices)
    /// </summary>
    [JsonPropertyName("transmitterId")]
    [Sanitizable]
    public string? TransmitterId { get; set; }

    /// <summary>
    /// Gets or sets the data source identifier indicating where this treatment originated from.
    /// Use constants from <see cref="Core.Constants.DataSources"/> for consistent values.
    /// </summary>
    /// <example>
    /// Common values: "demo-service", "dexcom-connector", "manual", "mongodb-import"
    /// </example>
    [JsonPropertyName("data_source")]
    public string? DataSource { get; set; }

    /// <summary>
    /// Gets or sets additional properties for the treatment
    /// </summary>
    [JsonPropertyName("additional_properties")]
    public Dictionary<string, object>? AdditionalProperties { get; set; }
}
