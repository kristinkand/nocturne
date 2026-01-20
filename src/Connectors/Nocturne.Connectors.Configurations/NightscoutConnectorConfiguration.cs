using System;
using System.ComponentModel.DataAnnotations;
using Nocturne.Connectors.Core.Extensions;
using Nocturne.Connectors.Core.Models;

#nullable enable
using Nocturne.Core.Constants;

namespace Nocturne.Connectors.Configurations
{
    /// <summary>
    /// Configuration specific to Nightscout-to-Nightscout connector
    /// </summary>
    [ConnectorRegistration(
        connectorName: "Nightscout",
        projectTypeName: "Nocturne_Connectors_Nightscout",
        serviceName: ServiceNames.NightscoutConnector,
        environmentPrefix: ServiceNames.ConnectorEnvironment.NightscoutPrefix,
        connectSourceName: "ConnectSource.Nightscout",
        dataSourceId: "nightscout-connector",
        icon: "nightscout",
        category: ConnectorCategory.Sync,
        description: "Sync with an existing Nightscout instance",
        displayName: "Nightscout"
    )]
    public class NightscoutConnectorConfiguration : BaseConnectorConfiguration
    {
        public NightscoutConnectorConfiguration()
        {
            ConnectSource = ConnectSource.Nightscout;
            BatchSize = 500;
        }

        /// <summary>
        /// Source Nightscout endpoint URL
        /// </summary>
        [Required]
        [EnvironmentVariable("CONNECT_NS_URL")]
        [AspireParameter("nightscout-source-url", "SourceEndpoint", secret: false, description: "Source Nightscout URL")]
        [RuntimeConfigurable("Source URL", "Connection")]
        [ConfigSchema(Format = "uri")]
        public string SourceEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Source Nightscout API secret (optional)
        /// </summary>
        [Secret]
        [EnvironmentVariable("CONNECT_NS_API_SECRET")]
        [AspireParameter("nightscout-source-secret", "SourceApiSecret", secret: true, description: "Source Nightscout API Secret", defaultValue: "")]
        public string? SourceApiSecret { get; set; }

        [Secret]
        [AspireParameter("nightscout-source-subject-token", "SubjectToken", secret: true, description: "Nightscout Subject Token for JWT Authentication", defaultValue: "")]
        public string? SubjectToken { get; set; }

        protected override void ValidateSourceSpecificConfiguration()
        {
            if (string.IsNullOrWhiteSpace(SourceEndpoint))
                throw new ArgumentException(
                    "CONNECT_NS_URL is required when using Nightscout source"
                );
        }
    }
}
