using System.ComponentModel.DataAnnotations;

namespace Nocturne.Tools.Abstractions.Configuration;

/// <summary>
/// Base interface for tool configuration.
/// </summary>
public interface IToolConfiguration
{
    /// <summary>
    /// Gets the name of the tool.
    /// </summary>
    string ToolName { get; }

    /// <summary>
    /// Gets the version of the tool.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <returns>A validation result indicating whether the configuration is valid.</returns>
    ValidationResult ValidateConfiguration();
}
