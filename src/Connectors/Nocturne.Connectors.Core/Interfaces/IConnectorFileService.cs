using System;
using System.Threading.Tasks;
using Nocturne.Connectors.Core.Models;

#nullable enable

namespace Nocturne.Connectors.Core.Interfaces
{
    /// <summary>
    /// Generic interface for saving and loading connector data to/from files for debugging purposes
    /// </summary>
    /// <typeparam name="TData">The type of data to save/load</typeparam>
    public interface IConnectorFileService<TData>
        where TData : class
    {
        /// <summary>
        /// Save connector data to a timestamped file
        /// </summary>
        /// <param name="data">The data to save</param>
        /// <param name="dataDirectory">Directory to save the file in</param>
        /// <param name="filePrefix">Prefix for the filename (e.g., "glooko_batch")</param>
        /// <returns>Path to the saved file if successful, null otherwise</returns>
        Task<string?> SaveDataAsync(TData data, string dataDirectory, string filePrefix);

        /// <summary>
        /// Load connector data from a specific file
        /// </summary>
        /// <param name="filePath">Path to the file to load</param>
        /// <returns>The loaded data if successful, null otherwise</returns>
        Task<TData?> LoadDataAsync(string filePath);

        /// <summary>
        /// Get available data files in the specified directory with the given prefix
        /// </summary>
        /// <param name="dataDirectory">Directory to search</param>
        /// <param name="filePrefix">File prefix to match (e.g., "glooko_batch")</param>
        /// <returns>Array of file paths, sorted chronologically</returns>
        string[] GetAvailableDataFiles(string dataDirectory, string filePrefix);

        /// <summary>
        /// Get the most recent data file in the directory with the given prefix
        /// </summary>
        /// <param name="dataDirectory">Directory to search</param>
        /// <param name="filePrefix">File prefix to match</param>
        /// <returns>Path to the most recent file, or null if none found</returns>
        string? GetMostRecentDataFile(string dataDirectory, string filePrefix);
    }
}
