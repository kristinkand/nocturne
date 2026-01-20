using System;
using System.ComponentModel.DataAnnotations;
using Nocturne.Connectors.Core.Extensions;
using Nocturne.Connectors.Core.Models;

#nullable enable
using Nocturne.Core.Constants;

namespace Nocturne.Connectors.Configurations
{
    /// <summary>
    /// Configuration specific to Glooko connector
    /// </summary>
    [ConnectorRegistration(
        connectorName: "Glooko",
        projectTypeName: "Nocturne_Connectors_Glooko",
        serviceName: ServiceNames.GlookoConnector,
        environmentPrefix: ServiceNames.ConnectorEnvironment.GlookoPrefix,
        connectSourceName: "ConnectSource.Glooko",
        dataSourceId: "glooko-connector",
        icon: "glooko",
        category: ConnectorCategory.Sync,
        description: "Import data from Glooko platform",
        displayName: "Glooko"
    )]
    public class GlookoConnectorConfiguration : BaseConnectorConfiguration
    {
        public GlookoConnectorConfiguration()
        {
            ConnectSource = ConnectSource.Glooko;
        }

        /// <summary>
        /// Glooko account email
        /// </summary>
        [Required]
        [EnvironmentVariable("CONNECT_GLOOKO_USERNAME")]
        [AspireParameter(
            "glooko-username",
            "Email",
            secret: false,
            description: "Glooko account email"
        )]
        [RuntimeConfigurable("Email", "Connection")]
        public string GlookoUsername { get; set; } = string.Empty;

        /// <summary>
        /// Glooko account password
        /// </summary>
        [Required]
        [Secret]
        [EnvironmentVariable("CONNECT_GLOOKO_PASSWORD")]
        [AspireParameter(
            "glooko-password",
            "Password",
            secret: true,
            description: "Glooko account password"
        )]
        public string GlookoPassword { get; set; } = string.Empty;

        /// <summary>
        /// Glooko server region (US or EU)
        /// </summary>
        [EnvironmentVariable("CONNECT_GLOOKO_SERVER")]
        [AspireParameter(
            "glooko-server",
            "Server",
            secret: false,
            description: "Glooko server (US or EU)",
            defaultValue: "US"
        )]
        [RuntimeConfigurable("Server", "Connection")]
        [ConfigSchema(Enum = new[] { "US", "EU" })]
        public string GlookoServer { get; set; } = "US";

        /// <summary>
        /// Use v3 API for additional data types (alarms, automatic boluses, consumables).
        /// This provides a single API call instead of multiple v2 calls.
        /// </summary>
        [EnvironmentVariable("CONNECT_GLOOKO_USE_V3_API")]
        [RuntimeConfigurable("Use V3 API", "Advanced")]
        public bool UseV3Api { get; set; } = true;

        /// <summary>
        /// Include CGM readings from v3 as backup to primary CGM source (e.g., xDrip).
        /// Only use this if you want Glooko to fill gaps in your primary CGM data.
        /// </summary>
        [EnvironmentVariable("CONNECT_GLOOKO_V3_CGM_BACKFILL")]
        [RuntimeConfigurable("CGM Backfill", "Advanced")]
        public bool V3IncludeCgmBackfill { get; set; } = false;

        protected override void ValidateSourceSpecificConfiguration()
        {
            if (string.IsNullOrWhiteSpace(GlookoUsername))
                throw new ArgumentException(
                    "CONNECT_GLOOKO_USERNAME is required when using Glooko source"
                );

            if (string.IsNullOrWhiteSpace(GlookoPassword))
                throw new ArgumentException(
                    "CONNECT_GLOOKO_PASSWORD is required when using Glooko source"
                );
        }
    }
}
