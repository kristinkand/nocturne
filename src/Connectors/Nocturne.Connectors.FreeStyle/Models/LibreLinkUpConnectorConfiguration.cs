using System;
using System.ComponentModel.DataAnnotations;
using Nocturne.Connectors.Core.Extensions;
using Nocturne.Connectors.Core.Models;

#nullable enable

namespace Nocturne.Connectors.FreeStyle.Models
{
    /// <summary>
    /// Configuration specific to LibreLinkUp connector
    /// </summary>
    public class LibreLinkUpConnectorConfiguration : BaseConnectorConfiguration
    {
        public LibreLinkUpConnectorConfiguration()
        {
            ConnectSource = ConnectSource.LibreLinkUp;
        }

        /// <summary>
        /// LibreLinkUp username
        /// </summary>
        [Required]
        [EnvironmentVariable("CONNECT_LIBRE_USERNAME")]
        public string LibreUsername { get; set; } = string.Empty;

        /// <summary>
        /// LibreLinkUp password
        /// </summary>
        [Required]
        [EnvironmentVariable("CONNECT_LIBRE_PASSWORD")]
        public string LibrePassword { get; set; } = string.Empty;

        /// <summary>
        /// LibreLinkUp region
        /// </summary>
        [EnvironmentVariable("CONNECT_LIBRE_REGION")]
        public string LibreRegion { get; set; } = "EU";

        /// <summary>
        /// LibreLinkUp server URL (optional)
        /// </summary>
        public string LibreServer { get; set; } = string.Empty;

        /// <summary>
        /// Patient ID for LibreLinkUp (for caregiver accounts)
        /// </summary>
        [EnvironmentVariable("CONNECT_LIBRE_PATIENT_ID")]
        public string LibrePatientId { get; set; } = string.Empty;

        protected override void ValidateSourceSpecificConfiguration()
        {
            if (string.IsNullOrWhiteSpace(LibreUsername))
                throw new ArgumentException(
                    "CONNECT_LINK_UP_USERNAME is required when using LibreLinkUp source"
                );

            if (string.IsNullOrWhiteSpace(LibrePassword))
                throw new ArgumentException(
                    "CONNECT_LINK_UP_PASSWORD is required when using LibreLinkUp source"
                );
        }
    }
}
