using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nocturne.Tools.Abstractions.Configuration;
using Nocturne.Tools.Abstractions.Services;
using Nocturne.Tools.Core.Configuration;
using Nocturne.Tools.Core.Services;
using Spectre.Console.Cli;

namespace Nocturne.Tools.Core;

/// <summary>
/// Builder for creating Spectre.Console CLI applications with common infrastructure.
/// </summary>
public class SpectreApplicationBuilder
{
    private readonly CommandApp _app;
    private readonly IServiceCollection _services;
    private readonly string _toolName;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpectreApplicationBuilder"/> class.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    public SpectreApplicationBuilder(string toolName)
    {
        _toolName = toolName;
        _services = new ServiceCollection();
        _app = new CommandApp(new TypeRegistrar(_services));
    }

    /// <summary>
    /// Configures logging for the application.
    /// </summary>
    /// <param name="configureLogging">Optional logging configuration action.</param>
    /// <returns>The builder instance.</returns>
    public SpectreApplicationBuilder ConfigureLogging(
        Action<ILoggingBuilder>? configureLogging = null
    )
    {
        _services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
            configureLogging?.Invoke(builder);
        });

        return this;
    }

    /// <summary>
    /// Configures the core services for the application.
    /// </summary>
    /// <returns>The builder instance.</returns>
    public SpectreApplicationBuilder ConfigureCoreServices()
    {
        _services.AddSingleton<IConfigurationManager, ConfigurationManager>();
        _services.AddSingleton<IValidationService, ValidationService>();
        _services.AddSingleton<IProgressReporter, ConsoleProgressReporter>();
        _services.AddSingleton<IConnectionTestService, ConnectionTestService>();

        return this;
    }

    /// <summary>
    /// Configures additional services for the application.
    /// </summary>
    /// <param name="configureServices">The service configuration action.</param>
    /// <returns>The builder instance.</returns>
    public SpectreApplicationBuilder ConfigureServices(Action<IServiceCollection> configureServices)
    {
        configureServices(_services);
        return this;
    }

    /// <summary>
    /// Configures the application with default settings.
    /// </summary>
    /// <param name="configure">Configuration action for the CommandApp.</param>
    /// <returns>The builder instance.</returns>
    public SpectreApplicationBuilder Configure(Action<IConfigurator> configure)
    {
        _app.Configure(config =>
        {
            config.SetApplicationName(_toolName);
            config.ValidateExamples();
            configure(config);
        });

        return this;
    }

    /// <summary>
    /// Builds the Spectre.Console CLI application.
    /// </summary>
    /// <returns>The built application.</returns>
    public CommandApp Build()
    {
        return _app;
    }

    /// <summary>
    /// Creates a new builder instance.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <returns>A new builder instance.</returns>
    public static SpectreApplicationBuilder Create(string toolName)
    {
        return new SpectreApplicationBuilder(toolName);
    }
}
