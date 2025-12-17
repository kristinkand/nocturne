using System.Text.Json.Serialization;

#nullable enable

namespace Nocturne.Connectors.Glooko.Models
{
    public class GlookoV3GraphData
    {
        [JsonPropertyName("series")]
        public GlookoV3Series? Series { get; set; }
    }

    public class GlookoV3Series
    {
        [JsonPropertyName("reservoirChange")]
        public GlookoReservoirChange[]? ReservoirChanges { get; set; }
    }

    public class GlookoReservoirChange
    {
        [JsonPropertyName("x")]
        public long X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }
}
