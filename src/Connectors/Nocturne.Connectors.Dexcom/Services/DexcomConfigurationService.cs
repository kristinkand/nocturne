using System;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Services;
using Nocturne.Connectors.Dexcom.Models;

namespace Nocturne.Connectors.Dexcom.Services
{
    /// <summary>
    /// Configuration service specific to Dexcom connector
    /// </summary>
    public class DexcomConfigurationService : BaseConfigurationService
    {
        public override IConnectorConfiguration LoadConfiguration()
        {
            LoadEnvironmentFile();

            var config = new DexcomConnectorConfiguration();

            PopulateBaseConfiguration(config);
            PopulateDexcomSpecificConfiguration(config);
            ValidateConfiguration(config);

            return config;
        }

        private static void PopulateDexcomSpecificConfiguration(DexcomConnectorConfiguration config)
        {
            config.DexcomUsername =
                Environment.GetEnvironmentVariable("CONNECT_SHARE_ACCOUNT_NAME") ?? string.Empty;
            config.DexcomPassword =
                Environment.GetEnvironmentVariable("CONNECT_SHARE_PASSWORD") ?? string.Empty;
            config.DexcomRegion =
                Environment.GetEnvironmentVariable("CONNECT_SHARE_REGION") ?? "us";
            config.DexcomServer =
                Environment.GetEnvironmentVariable("CONNECT_SHARE_SERVER") ?? string.Empty;
        }
    }
}
