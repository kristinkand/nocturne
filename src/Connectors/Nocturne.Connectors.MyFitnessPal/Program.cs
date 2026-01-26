using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.Connectors.Configurations;
using Nocturne.Connectors.Core.Extensions;
using Nocturne.Connectors.Core.Health;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.MyFitnessPal.Models;
using Nocturne.Connectors.MyFitnessPal.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Nocturne.Connectors.MyFitnessPal;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        if (!builder.Configuration.IsConnectorEnabled("MyFitnessPal")) return;

        // Add service defaults
        builder.AddServiceDefaults();

        // Configure services
        // Bind configuration for HttpClient setup
        var mfpConfig = new MyFitnessPalConnectorConfiguration();
        builder.Configuration.BindConnectorConfiguration(
            mfpConfig,
            "MyFitnessPal",
            builder.Environment.ContentRootPath
        );

        // Register the fully bound configuration instance
        builder.Services.AddSingleton<IOptions<MyFitnessPalConnectorConfiguration>>(
            new OptionsWrapper<MyFitnessPalConnectorConfiguration>(mfpConfig)
        );
        builder.Services.AddSingleton(mfpConfig);

        builder.Services.AddHttpClient<MyFitnessPalConnectorService>()
            .ConfigureMyFitnessPalClient();



        // Configure API data submitter for HTTP-based data submission
        builder.Services.AddConnectorApiDataSubmitter(builder.Configuration);

        builder.Services.AddHostedService<MyFitnessPalSyncService>();

        // Add health checks
        // Add base connector services (State, Metrics, Strategies)
        builder.Services.AddBaseConnectorServices();
        builder.Services.AddHealthChecks().AddConnectorHealthCheck("myfitnesspal");

        var app = builder.Build();

        // Map default endpoints (includes health checks in development)
        app.MapDefaultEndpoints();

        // Configure standard connector endpoints (Sync, Capabilities, Health/Data)
        app.MapConnectorEndpoints<MyFitnessPalConnectorService, MyFitnessPalConnectorConfiguration>("MyFitnessPal Connector");

        // Configure graceful shutdown
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Starting MyFitnessPal Connector Service...");

        await app.RunAsync();
    }
}
