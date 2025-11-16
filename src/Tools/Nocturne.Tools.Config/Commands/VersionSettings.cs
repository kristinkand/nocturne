using System.ComponentModel;
using Spectre.Console.Cli;

namespace Nocturne.Tools.Config.Commands;

/// <summary>
/// Settings for the version command.
/// </summary>
public sealed class VersionSettings : CommandSettings
{
    /// <summary>
    /// Gets or sets whether to show detailed version information.
    /// </summary>
    [CommandOption("-d|--detailed")]
    [Description("Whether to show detailed version information")]
    [DefaultValue(false)]
    public bool Detailed { get; set; } = false;
}
