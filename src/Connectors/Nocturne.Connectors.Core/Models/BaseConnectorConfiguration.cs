using System;
using System.ComponentModel.DataAnnotations;
using Nocturne.Connectors.Core.Interfaces;

#nullable enable

namespace Nocturne.Connectors.Core.Models
{
    /// <summary>
    /// Base implementation of connector configuration with common properties
    /// </summary>
    public abstract class BaseConnectorConfiguration : IConnectorConfiguration
    {
        [Required]
        public ConnectSource ConnectSource { get; set; }

        public bool SaveRawData { get; set; } = false;

        public string DataDirectory { get; set; } = "./data";

        public bool LoadFromFile { get; set; } = false;

        public string? LoadFilePath { get; set; }

        public bool DeleteAfterUpload { get; set; } = false;

        public bool UseAsyncProcessing { get; set; } = true;

        public bool FallbackToDirectApi { get; set; } = true;

        public TimeSpan MessageTimeout { get; set; } = TimeSpan.FromMinutes(5);

        public int MaxRetryAttempts { get; set; } = 3;

        public int BatchSize { get; set; } = 50;

        public string? RoutingKeyPrefix { get; set; }

        public int SyncIntervalMinutes { get; set; } = 5;

        public ConnectorMode Mode { get; set; } = ConnectorMode.Standalone;

        public string NightscoutUrl { get; set; } = string.Empty;

        public string NightscoutApiSecret { get; set; } = string.Empty;

        public string ApiSecret { get; set; } = string.Empty;

        public virtual void Validate()
        {
            if (!Enum.IsDefined(typeof(ConnectSource), ConnectSource))
                throw new ArgumentException($"Invalid connector source: {ConnectSource}");

            // Validate mode-specific requirements
            ValidateModeSpecificConfiguration();

            ValidateSourceSpecificConfiguration();
        }

        /// <summary>
        /// Validates configuration based on the connector mode
        /// </summary>
        protected virtual void ValidateModeSpecificConfiguration()
        {
            switch (Mode)
            {
                case ConnectorMode.Standalone:
                    if (string.IsNullOrWhiteSpace(NightscoutUrl))
                        throw new ArgumentException(
                            "Nightscout URL is required for Standalone mode"
                        );

                    if (
                        string.IsNullOrWhiteSpace(NightscoutApiSecret)
                        && string.IsNullOrWhiteSpace(ApiSecret)
                    )
                        throw new ArgumentException(
                            "Either NightscoutApiSecret or ApiSecret is required for Standalone mode authentication"
                        );
                    break;

                case ConnectorMode.Nocturne:
                    // In Nocturne mode, Nightscout URL and API secret are optional
                    // as the connector works via message bus and Nocturne handles Nightscout communication
                    break;

                default:
                    throw new ArgumentException($"Unknown connector mode: {Mode}");
            }
        }

        /// <summary>
        /// Override this method to validate connector-specific configuration
        /// </summary>
        protected abstract void ValidateSourceSpecificConfiguration();
    }
}
