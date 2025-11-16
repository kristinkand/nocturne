using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;

namespace Nocturne.Connectors.Core.Services
{
    /// <summary>
    /// Factory for creating connector services based on configuration
    /// This factory now works with the generic connector interfaces and specific configuration types
    /// </summary>
    public class ConnectorFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        public ConnectorFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory =
                loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        /// <summary>
        /// Parse connect source string to enum
        /// NOTE: Configuration creation is now handled by individual connector projects
        /// </summary>
        public ConnectSource ParseConnectSource(string connectSource)
        {
            if (string.IsNullOrWhiteSpace(connectSource))
                throw new ArgumentException(
                    "Connect source cannot be null or empty",
                    nameof(connectSource)
                );

            return ConnectorConfigurationFactory.ParseConnectSource(connectSource);
        }

        /// <summary>
        /// Get all supported connector sources
        /// </summary>
        public static string[] GetSupportedSources()
        {
            return ConnectorConfigurationFactory.GetSupportedSources();
        }

        /// <summary>
        /// Validate that a connector source is supported
        /// </summary>
        public static bool IsValidSource(string source)
        {
            return ConnectorConfigurationFactory.IsValidSource(source);
        }
    }
}
