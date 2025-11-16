using Microsoft.Extensions.Configuration;

namespace Nocturne.Tools.Abstractions.Configuration;

/// <summary>
/// Manages configuration loading and validation for tools.
/// </summary>
public interface IConfigurationManager
{
    /// <summary>
    /// Loads configuration from multiple sources.
    /// </summary>
    /// <param name="configurationPath">Optional path to configuration file.</param>
    /// <returns>The loaded configuration.</returns>
    IConfiguration LoadConfiguration(string? configurationPath = null);

    /// <summary>
    /// Loads and binds configuration to a specific type.
    /// </summary>
    /// <typeparam name="T">The configuration type to bind to.</typeparam>
    /// <param name="configurationPath">Optional path to configuration file.</param>
    /// <returns>The bound configuration object.</returns>
    T LoadConfiguration<T>(string? configurationPath = null)
        where T : class, IToolConfiguration, new();

    /// <summary>
    /// Validates a configuration object.
    /// </summary>
    /// <param name="configuration">The configuration to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    bool ValidateConfiguration(IToolConfiguration configuration);

    /// <summary>
    /// Creates a configuration template file.
    /// </summary>
    /// <param name="outputPath">The path to write the template.</param>
    /// <param name="toolName">The name of the tool.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CreateConfigurationTemplateAsync(string outputPath, string toolName);
}
