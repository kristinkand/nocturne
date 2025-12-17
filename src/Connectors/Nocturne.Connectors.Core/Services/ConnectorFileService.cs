using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Core.Interfaces;

#nullable enable

namespace Nocturne.Connectors.Core.Services
{
    /// <summary>
    /// Generic implementation for saving and loading connector data to/from JSON files
    /// </summary>
    /// <typeparam name="TData">The type of data to save/load</typeparam>
    public class ConnectorFileService<TData> : IConnectorFileService<TData>
        where TData : class
    {
        private readonly ILogger<ConnectorFileService<TData>> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public ConnectorFileService(ILogger<ConnectorFileService<TData>> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
        }

        /// <summary>
        /// Save connector data to a timestamped JSON file.
        /// Only saves if the content differs from the most recent file.
        /// </summary>
        public async Task<string?> SaveDataAsync(
            TData data,
            string dataDirectory,
            string filePrefix
        )
        {
            try
            {
                if (!Directory.Exists(dataDirectory))
                {
                    Directory.CreateDirectory(dataDirectory);
                    _logger.LogInformation(
                        "Created data directory: {DataDirectory}",
                        dataDirectory
                    );
                }

                var json = JsonSerializer.Serialize(data, _jsonOptions);

                // Check if we already have a file with the same content
                var mostRecentFile = GetMostRecentDataFile(dataDirectory, filePrefix);
                if (mostRecentFile != null)
                {
                    var existingContent = await File.ReadAllTextAsync(mostRecentFile);
                    if (existingContent == json)
                    {
                        _logger.LogDebug(
                            "Skipping save - {DataType} data unchanged from: {FilePath}",
                            typeof(TData).Name,
                            mostRecentFile
                        );
                        return mostRecentFile;
                    }
                }

                var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
                var fileName = $"{filePrefix}_{timestamp}.json";
                var filePath = Path.Combine(dataDirectory, fileName);

                await File.WriteAllTextAsync(filePath, json);

                _logger.LogInformation(
                    "Saved {DataType} data to: {FilePath}",
                    typeof(TData).Name,
                    filePath
                );
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving {DataType} data to file", typeof(TData).Name);
                return null;
            }
        }

        /// <summary>
        /// Load connector data from a JSON file
        /// </summary>
        public async Task<TData?> LoadDataAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogError("File not found: {FilePath}", filePath);
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var data = JsonSerializer.Deserialize<TData>(json, _jsonOptions);

                if (data != null)
                {
                    _logger.LogInformation(
                        "Loaded {DataType} data from: {FilePath}",
                        typeof(TData).Name,
                        filePath
                    );
                }

                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading {DataType} data from file: {FilePath}",
                    typeof(TData).Name,
                    filePath
                );
                return null;
            }
        }

        /// <summary>
        /// Get available data files in the specified directory with the given prefix
        /// </summary>
        public string[] GetAvailableDataFiles(string dataDirectory, string filePrefix)
        {
            try
            {
                if (!Directory.Exists(dataDirectory))
                {
                    return Array.Empty<string>();
                }

                var searchPattern = $"{filePrefix}_*.json";
                var files = Directory.GetFiles(
                    dataDirectory,
                    searchPattern,
                    SearchOption.TopDirectoryOnly
                );
                Array.Sort(files); // Sort alphabetically (which should be chronological due to timestamp format)
                return files;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error retrieving {DataType} data files from: {DataDirectory}",
                    typeof(TData).Name,
                    dataDirectory
                );
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Get the most recent data file in the directory with the given prefix
        /// </summary>
        public string? GetMostRecentDataFile(string dataDirectory, string filePrefix)
        {
            var files = GetAvailableDataFiles(dataDirectory, filePrefix);
            return files.Length > 0 ? files[^1] : null; // Return the last (most recent) file
        }
    }
}
