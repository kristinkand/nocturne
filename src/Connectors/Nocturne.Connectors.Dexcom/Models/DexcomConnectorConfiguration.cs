using System;
using System.ComponentModel.DataAnnotations;
using Nocturne.Connectors.Core.Models;

#nullable enable

namespace Nocturne.Connectors.Dexcom.Models
{
    /// <summary>
    /// Configuration specific to Dexcom Share connector
    /// </summary>
    public class DexcomConnectorConfiguration : BaseConnectorConfiguration
    {
        public DexcomConnectorConfiguration()
        {
            ConnectSource = ConnectSource.Dexcom;
        }

        /// <summary>
        /// Dexcom Share username
        /// </summary>
        [Required]
        public string DexcomUsername { get; set; } = string.Empty;

        /// <summary>
        /// Dexcom Share password
        /// </summary>
        [Required]
        public string DexcomPassword { get; set; } = string.Empty;

        /// <summary>
        /// Dexcom region ("us" or "ous")
        /// </summary>
        public string DexcomRegion { get; set; } = "us";

        /// <summary>
        /// Custom Dexcom server (optional, overrides region-based server selection)
        /// </summary>
        public string DexcomServer { get; set; } = string.Empty;

        protected override void ValidateSourceSpecificConfiguration()
        {
            if (string.IsNullOrWhiteSpace(DexcomUsername))
                throw new ArgumentException(
                    "CONNECT_SHARE_ACCOUNT_NAME is required when using Dexcom Share source"
                );

            if (string.IsNullOrWhiteSpace(DexcomPassword))
                throw new ArgumentException(
                    "CONNECT_SHARE_PASSWORD is required when using Dexcom Share source"
                );
        }
    }
}
