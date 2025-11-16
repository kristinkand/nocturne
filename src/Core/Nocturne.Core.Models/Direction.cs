using System.Text.Json.Serialization;

namespace Nocturne.Core.Models;

/// <summary>
/// Represents the glucose trend direction indicators used by Nightscout.
/// These values indicate the rate and direction of glucose change.
/// 1:1 Legacy JavaScript compatibility with ClientApp/lib/plugins/direction.js
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Direction
{
    /// <summary>
    /// No direction information available
    /// </summary>
    NONE,

    /// <summary>
    /// Rising very rapidly (>3 mg/dL per minute)
    /// </summary>
    TripleUp,

    /// <summary>
    /// Rising rapidly (2-3 mg/dL per minute)
    /// </summary>
    DoubleUp,

    /// <summary>
    /// Rising (1-2 mg/dL per minute)
    /// </summary>
    SingleUp,

    /// <summary>
    /// Rising slowly (0.5-1 mg/dL per minute)
    /// </summary>
    FortyFiveUp,

    /// <summary>
    /// Stable (change less than 0.5 mg/dL per minute)
    /// </summary>
    Flat,

    /// <summary>
    /// Falling slowly (0.5-1 mg/dL per minute)
    /// </summary>
    FortyFiveDown,

    /// <summary>
    /// Falling (1-2 mg/dL per minute)
    /// </summary>
    SingleDown,

    /// <summary>
    /// Falling rapidly (2-3 mg/dL per minute)
    /// </summary>
    DoubleDown,

    /// <summary>
    /// Falling very rapidly (>3 mg/dL per minute)
    /// </summary>
    TripleDown,

    /// <summary>
    /// CGM cannot determine direction due to insufficient data
    /// </summary>
    [JsonPropertyName("NOT COMPUTABLE")]
    NotComputable,

    /// <summary>
    /// Rate of change is outside measurable range
    /// </summary>
    [JsonPropertyName("RATE OUT OF RANGE")]
    RateOutOfRange,

    /// <summary>
    /// CGM sensor error or malfunction
    /// </summary>
    [JsonPropertyName("CGM ERROR")]
    CgmError,
}
