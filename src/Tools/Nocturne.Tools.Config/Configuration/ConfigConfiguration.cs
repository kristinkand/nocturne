using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Nocturne.Tools.Abstractions.Configuration;

namespace Nocturne.Tools.Config.Configuration;

/// <summary>
/// Configuration for the Nocturne Config tool.
/// </summary>
public class ConfigConfiguration : IToolConfiguration
{
    /// <inheritdoc/>
    public string ToolName => "Nocturne Config";

    /// <inheritdoc/>
    public string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

    /// <summary>
    /// Output file path for generated configuration.
    /// </summary>
    [Required(ErrorMessage = "Output path is required")]
    public string OutputPath { get; set; } = "appsettings.example.json";

    /// <summary>
    /// Whether to overwrite existing files.
    /// </summary>
    public bool OverwriteExisting { get; set; } = false;

    /// <summary>
    /// Whether to include comments in the generated configuration.
    /// </summary>
    public bool IncludeComments { get; set; } = true;

    /// <summary>
    /// Configuration format to generate.
    /// </summary>
    [Required(ErrorMessage = "Configuration format is required")]
    public ConfigFormat Format { get; set; } = ConfigFormat.Json;

    /// <summary>
    /// Configuration environment to generate for.
    /// </summary>
    public string Environment { get; set; } = "Development";

    /// <inheritdoc/>
    public ValidationResult ValidateConfiguration()
    {
        var validFormats = Enum.GetValues<ConfigFormat>();

        if (!validFormats.Contains(Format))
        {
            return new ValidationResult(
                $"Invalid configuration format '{Format}'. Valid formats are: {string.Join(", ", validFormats)}"
            );
        }

        return ValidationResult.Success!;
    }
}

/// <summary>
/// Configuration format enumeration.
/// </summary>
public enum ConfigFormat
{
    /// <summary>
    /// JSON format (appsettings.json)
    /// </summary>
    Json,

    /// <summary>
    /// Environment variables format (.env)
    /// </summary>
    EnvironmentVariables,

    /// <summary>
    /// YAML format (appsettings.yml)
    /// </summary>
    Yaml,
}
