using System;
using System.ComponentModel.DataAnnotations;
using Nocturne.Connectors.Core.Extensions;
using Nocturne.Connectors.Core.Models;

#nullable enable

namespace Nocturne.Connectors.Nightscout.Models
{
    /// <summary>
    /// Configuration specific to Nightscout-to-Nightscout connector
    /// </summary>
    public class NightscoutConnectorConfiguration : BaseConnectorConfiguration
    {
        public NightscoutConnectorConfiguration()
        {
            ConnectSource = ConnectSource.Nightscout;
        }

        /// <summary>
        /// Source Nightscout endpoint URL
        /// </summary>
        [Required]
        [EnvironmentVariable("CONNECT_NS_URL")]
        public string SourceEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Source Nightscout API secret (optional)
        /// </summary>
        [EnvironmentVariable("CONNECT_NS_API_SECRET")]
        public string? SourceApiSecret { get; set; }

        protected override void ValidateSourceSpecificConfiguration()
        {
            if (string.IsNullOrWhiteSpace(SourceEndpoint))
                throw new ArgumentException(
                    "CONNECT_SOURCE_ENDPOINT is required when using Nightscout source"
                );
        }
    }
}
