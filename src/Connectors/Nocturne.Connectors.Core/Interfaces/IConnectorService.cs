using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nocturne.Core.Models;

namespace Nocturne.Connectors.Core.Interfaces
{
    /// <summary>
    /// Interface for data source connector services that fetch glucose data from various platforms
    /// </summary>
    /// <typeparam name="TConfig">The connector-specific configuration type</typeparam>
    public interface IConnectorService<TConfig> : IDisposable
        where TConfig : IConnectorConfiguration
    {
        /// <summary>
        /// Authenticate with the data source
        /// </summary>
        Task<bool> AuthenticateAsync();

        /// <summary>
        /// Fetch glucose entries from the data source
        /// </summary>
        /// <param name="since">Fetch entries since this timestamp (optional)</param>
        Task<IEnumerable<Entry>> FetchGlucoseDataAsync(DateTime? since = null);

        /// <summary>
        /// Upload Entry models to Nightscout
        /// </summary>
        Task<bool> UploadToNightscoutAsync(IEnumerable<Entry> entries, TConfig config);

        /// <summary>
        /// Get the name of this connector service
        /// </summary>
        string ServiceName { get; }
    }
}
