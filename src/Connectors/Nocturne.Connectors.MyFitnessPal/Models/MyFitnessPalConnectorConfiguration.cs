using System;
using System.ComponentModel.DataAnnotations;
using Nocturne.Connectors.Core.Extensions;
using Nocturne.Connectors.Core.Models;

#nullable enable

namespace Nocturne.Connectors.MyFitnessPal.Models
{
    /// <summary>
    /// Configuration specific to MyFitnessPal connector
    /// </summary>
    public class MyFitnessPalConnectorConfiguration : BaseConnectorConfiguration
    {
        public MyFitnessPalConnectorConfiguration()
        {
            ConnectSource = ConnectSource.MyFitnessPal;
        }

        /// <summary>
        /// MyFitnessPal username/email
        /// </summary>
        [Required]
        [EnvironmentVariable("CONNECT_MFP_USERNAME")]
        public string MyFitnessPalUsername { get; set; } = string.Empty;

        /// <summary>
        /// MyFitnessPal password
        /// </summary>
        [Required]
        [EnvironmentVariable("CONNECT_MFP_PASSWORD")]
        public string MyFitnessPalPassword { get; set; } = string.Empty;

        /// <summary>
        /// MyFitnessPal API key (if available)
        /// </summary>
        [EnvironmentVariable("CONNECT_MFP_API_KEY")]
        public string MyFitnessPalApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Number of days to sync from MyFitnessPal (defaults to 7)
        /// </summary>
        public int SyncDays { get; set; } = 7;

        protected override void ValidateSourceSpecificConfiguration()
        {
            if (string.IsNullOrWhiteSpace(MyFitnessPalUsername))
                throw new ArgumentException(
                    "CONNECT_MYFITNESSPAL_USERNAME is required when using MyFitnessPal source"
                );

            if (string.IsNullOrWhiteSpace(MyFitnessPalPassword))
                throw new ArgumentException(
                    "CONNECT_MYFITNESSPAL_PASSWORD is required when using MyFitnessPal source"
                );
        }
    }
}
