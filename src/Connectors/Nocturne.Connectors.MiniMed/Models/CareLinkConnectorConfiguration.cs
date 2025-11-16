using System;
using System.ComponentModel.DataAnnotations;
using Nocturne.Connectors.Core.Models;

#nullable enable

namespace Nocturne.Connectors.MiniMed.Models
{
    /// <summary>
    /// Configuration specific to MiniMed CareLink connector
    /// </summary>
    public class CareLinkConnectorConfiguration : BaseConnectorConfiguration
    {
        public CareLinkConnectorConfiguration()
        {
            ConnectSource = ConnectSource.CareLink;
        }

        /// <summary>
        /// CareLink username
        /// </summary>
        [Required]
        public string CarelinkUsername { get; set; } = string.Empty;

        /// <summary>
        /// CareLink password
        /// </summary>
        [Required]
        public string CarelinkPassword { get; set; } = string.Empty;

        /// <summary>
        /// CareLink region
        /// </summary>
        public string CarelinkRegion { get; set; } = "us";

        /// <summary>
        /// Country code for CareLink
        /// </summary>
        public string CarelinkCountryCode { get; set; } = string.Empty;

        /// <summary>
        /// Patient username for CareLink (for caregiver accounts)
        /// </summary>
        public string CarelinkPatientUsername { get; set; } = string.Empty;

        protected override void ValidateSourceSpecificConfiguration()
        {
            if (string.IsNullOrWhiteSpace(CarelinkUsername))
                throw new ArgumentException(
                    "CONNECT_CARELINK_USERNAME is required when using MiniMed CareLink source"
                );

            if (string.IsNullOrWhiteSpace(CarelinkPassword))
                throw new ArgumentException(
                    "CONNECT_CARELINK_PASSWORD is required when using MiniMed CareLink source"
                );
        }
    }
}
