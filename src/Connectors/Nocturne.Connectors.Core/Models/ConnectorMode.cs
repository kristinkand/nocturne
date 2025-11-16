namespace Nocturne.Connectors.Core.Models
{
    /// <summary>
    /// Defines the operational mode for connectors
    /// </summary>
    public enum ConnectorMode
    {
        /// <summary>
        /// Standalone mode - connects directly to a Nightscout instance
        /// Requires NightscoutUrl and NightscoutApiSecret
        /// </summary>
        Standalone,

        /// <summary>
        /// Nocturne mode - works within the Nocturne ecosystem
        /// Uses message bus for data ingestion, does not require direct Nightscout connection
        /// </summary>
        Nocturne,
    }
}
