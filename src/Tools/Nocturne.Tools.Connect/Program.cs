using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nocturne.Tools.Connect.Commands;
using Nocturne.Tools.Connect.Configuration;
using Nocturne.Tools.Connect.Services;
using Nocturne.Tools.Core;
using Spectre.Console.Cli;

namespace Nocturne.Tools.Connect;

/// <summary>
/// Main entry point for the Nocturne Connect tool.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Add Connect-specific services
        services.AddHttpClient();
        services.AddSingleton<ConnectConfiguration>();
        services.AddScoped<ConnectorTestService>();
        services.AddScoped<ConnectorExecutionService>();
        services.AddScoped<DaemonStatusService>();

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            config
                .AddCommand<TestCommand>("test")
                .WithDescription("Test connections to data source and Nightscout");

            config
                .AddCommand<RunCommand>("run")
                .WithDescription("Run Nocturne Connect data synchronization");

            config
                .AddCommand<StatusCommand>("status")
                .WithDescription("Show current sync status and health");

            config
                .AddCommand<ConfigCommand>("config")
                .WithDescription("Display and validate configuration");

            config
                .AddCommand<InitCommand>("init")
                .WithDescription("Initialize Nocturne Connect configuration");

            config
                .AddCommand<VersionCommand>("version")
                .WithDescription("Display version information");
        });

        return await app.RunAsync(args);
    }
}
