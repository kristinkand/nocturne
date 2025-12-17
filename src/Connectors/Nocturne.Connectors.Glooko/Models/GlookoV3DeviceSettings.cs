using System.Text.Json.Serialization;

#nullable enable

namespace Nocturne.Connectors.Glooko.Models
{
    public class GlookoDevice
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("serialNumber")]
        public string? SerialNumber { get; set; }

        [JsonPropertyName("lastSyncTimestamp")]
        public string? LastSyncTimestamp { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    public class GlookoDevicesAndSettings
    {
        [JsonPropertyName("pumps")]
        public Dictionary<string, Dictionary<string, GlookoDeviceSettings>>? Pumps { get; set; }
    }

    public class GlookoDeviceSettings
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("syncTimestamp")]
        public string? SyncTimestamp { get; set; }

        [JsonPropertyName("displayableTimestamp")]
        public string? DisplayableTimestamp { get; set; }

        [JsonPropertyName("generalSettings")]
        public GlookoGeneralSettings? GeneralSettings { get; set; }

        [JsonPropertyName("profilesBolus")]
        public GlookoBolusProfile[]? ProfilesBolus { get; set; }

        [JsonPropertyName("pumpProfilesBasal")]
        public GlookoBasalProfile[]? PumpProfilesBasal { get; set; }
    }

    public class GlookoGeneralSettings
    {
        [JsonPropertyName("activeInsulinTime")]
        public double ActiveInsulinTime { get; set; }

        [JsonPropertyName("bgGoalHigh")]
        public double BgGoalHigh { get; set; }

        [JsonPropertyName("bgGoalLow")]
        public double BgGoalLow { get; set; }
    }

    public class GlookoBolusProfile
    {
        [JsonPropertyName("isfSegments")]
        public GlookoProfileSegment? IsfSegments { get; set; }

        [JsonPropertyName("targetBgSegments")]
        public GlookoProfileSegment? TargetBgSegments { get; set; }

        [JsonPropertyName("insulinToCarbRatioSegments")]
        public GlookoProfileSegment? InsulinToCarbRatioSegments { get; set; }
    }

    public class GlookoBasalProfile
    {
        [JsonPropertyName("segments")]
        public GlookoProfileSegment? Segments { get; set; }
    }

    public class GlookoProfileSegment
    {
        [JsonPropertyName("profileName")]
        public string? ProfileName { get; set; }

        [JsonPropertyName("current")]
        public bool Current { get; set; }

        [JsonPropertyName("data")]
        public GlookoProfileData[]? Data { get; set; }
    }

    public class GlookoProfileData
    {
        [JsonPropertyName("segmentStart")]
        public double SegmentStart { get; set; }

        [JsonPropertyName("duration")]
        public double Duration { get; set; }

        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("valueLow")]
        public double ValueLow { get; set; }

        [JsonPropertyName("valueHigh")]
        public double ValueHigh { get; set; }
    }
}
