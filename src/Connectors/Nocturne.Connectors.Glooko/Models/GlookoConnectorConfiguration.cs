using System;
using System.ComponentModel.DataAnnotations;
using Nocturne.Connectors.Core.Models;

#nullable enable

namespace Nocturne.Connectors.Glooko.Models
{
    /// <summary>
    /// Configuration specific to Glooko connector
    /// </summary>
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
        public string GlookoEmail { get; set; } = string.Empty;

        /// <summary>
        /// Glooko account password
        /// </summary>
        [Required]
        public string GlookoPassword { get; set; } = string.Empty;

        /// <summary>
        /// Timezone offset for Glooko data
        /// </summary>
        public int GlookoTimezoneOffset { get; set; } = 0;

        /// <summary>
        /// Glooko server URL (optional)
        /// </summary>
        public string GlookoServer { get; set; } = string.Empty;

        protected override void ValidateSourceSpecificConfiguration()
        {
            if (string.IsNullOrWhiteSpace(GlookoEmail))
                throw new ArgumentException(
                    "CONNECT_GLOOKO_EMAIL is required when using Glooko source"
                );

            if (string.IsNullOrWhiteSpace(GlookoPassword))
                throw new ArgumentException(
                    "CONNECT_GLOOKO_PASSWORD is required when using Glooko source"
                );
        }
    }
}
