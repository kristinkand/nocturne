using Microsoft.Extensions.DependencyInjection;
using Nocturne.Tools.Config.Commands;
using Nocturne.Tools.Config.Configuration;
using Nocturne.Tools.Config.Services;
using Nocturne.Tools.Core;
using Spectre.Console.Cli;

namespace Nocturne.Tools.Config;

/// <summary>
/// Main entry point for the Nocturne Config tool.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var app = SpectreApplicationBuilder
            .Create("Nocturne Config")
            .ConfigureLogging()
            .ConfigureCoreServices()
            .ConfigureServices(services =>
            {
                // Register Config-specific services
                services.AddSingleton<ConfigConfiguration>();
                services.AddTransient<ConfigurationGeneratorService>();
            })
            .Configure(config =>
            {
                config
                    .AddCommand<GenerateCommand>("generate")
                    .WithDescription("Generate configuration files with example values");

                config
                    .AddCommand<ValidateCommand>("validate")
                    .WithDescription("Validate configuration files");

                config
                    .AddCommand<VersionCommand>("version")
                    .WithDescription("Display version information for the Config tool");
            })
            .Build();

        return await app.RunAsync(args);
    }
}
