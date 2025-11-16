using System.ComponentModel;
using Spectre.Console.Cli;

namespace Nocturne.Tools.Config.Commands;

/// <summary>
/// Settings for the validate command.
/// </summary>
public sealed class ValidateSettings : CommandSettings
{
    /// <summary>
    /// Gets or sets the path to the configuration file to validate.
    /// </summary>
    [CommandOption("-c|--config")]
    [Description("Path to the configuration file to validate")]
    [DefaultValue("appsettings.json")]
    public string ConfigPath { get; set; } = "appsettings.json";

    /// <summary>
    /// Gets or sets whether to output detailed validation information.
    /// </summary>
    [CommandOption("-v|--verbose")]
    [Description("Whether to output detailed validation information")]
    [DefaultValue(false)]
    public bool Verbose { get; set; } = false;
}
