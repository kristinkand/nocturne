using System.ComponentModel;
using Nocturne.Tools.Config.Configuration;
using Spectre.Console.Cli;

namespace Nocturne.Tools.Config.Commands;

/// <summary>
/// Settings for the generate command.
/// </summary>
public sealed class GenerateSettings : CommandSettings
{
    /// <summary>
    /// Gets or sets the output file path for the generated configuration.
    /// </summary>
    [CommandOption("-o|--output")]
    [Description("Output file path for the generated configuration")]
    [DefaultValue("appsettings.example.json")]
    public string OutputPath { get; set; } = "appsettings.example.json";

    /// <summary>
    /// Gets or sets the configuration format to generate.
    /// </summary>
    [CommandOption("-f|--format")]
    [Description("Configuration format to generate (json, env, yaml)")]
    [DefaultValue(ConfigFormat.Json)]
    public ConfigFormat Format { get; set; } = ConfigFormat.Json;

    /// <summary>
    /// Gets or sets whether to overwrite existing files.
    /// </summary>
    [CommandOption("--overwrite")]
    [Description("Whether to overwrite existing files")]
    [DefaultValue(false)]
    public bool Overwrite { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to include helpful comments in the generated configuration.
    /// </summary>
    [CommandOption("-c|--comments")]
    [Description("Whether to include helpful comments in the generated configuration")]
    [DefaultValue(true)]
    public bool Comments { get; set; } = true;

    /// <summary>
    /// Gets or sets the configuration environment to generate for.
    /// </summary>
    [CommandOption("-e|--environment")]
    [Description("Configuration environment to generate for")]
    [DefaultValue("Development")]
    public string Environment { get; set; } = "Development";
}
