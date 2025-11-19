using System.Text.Json.Serialization;

namespace Nocturne.Connectors.Core.Models;

/// <summary>
/// Represents the operational mode of a connector
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConnectorMode
{
    /// <summary>
    /// Nocturne mode - connector communicates with Nocturne API
    /// </summary>
    Nocturne,

    /// <summary>
    /// Standalone mode - connector directly uploads to Nightscout
    /// </summary>
    Standalone,
}
