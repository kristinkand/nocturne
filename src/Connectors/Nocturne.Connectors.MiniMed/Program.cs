using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.Core.Services;
using Nocturne.Connectors.MiniMed.Models;
using Nocturne.Connectors.MiniMed.Services;

namespace Nocturne.Connectors.MiniMed;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add service defaults
        builder.AddServiceDefaults();

        // Configure services
        builder.Services.AddHttpClient();

        // Configure connector-specific services
        builder.Services.Configure<CareLinkConnectorConfiguration>(
            builder.Configuration.GetSection("Connectors:MiniMed")
        );

        // Configure API data submitter for HTTP-based data submission
        var apiUrl = builder.Configuration["NocturneApiUrl"];
        var apiSecret = builder.Configuration["ApiSecret"];

        builder.Services.AddSingleton<IApiDataSubmitter>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            var logger = sp.GetRequiredService<ILogger<ApiDataSubmitter>>();
            return new ApiDataSubmitter(httpClient, apiUrl, apiSecret, logger);
        });

        builder.Services.AddSingleton<CareLinkConnectorService>();
        builder.Services.AddHostedService<MiniMedHostedService>();

        // Add health checks
        builder.Services.AddHealthChecks().AddCheck<MiniMedHealthCheck>("minimed");

        var app = builder.Build();

        // Map default endpoints (includes health checks in development)
        app.MapDefaultEndpoints();

        // Configure manual sync endpoint
        app.MapPost(
            "/sync",
            async (IServiceProvider serviceProvider, CancellationToken cancellationToken) =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                var config = serviceProvider
                    .GetRequiredService<IOptionsSnapshot<CareLinkConnectorConfiguration>>()
                    .Value;

                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var connectorService =
                        scope.ServiceProvider.GetRequiredService<CareLinkConnectorService>();

                    logger.LogInformation("Manual sync triggered for MiniMed connector");
                    var success = await connectorService.SyncCareLinkDataAsync(
                        config,
                        cancellationToken
                    );

                    return Results.Ok(
                        new
                        {
                            success,
                            message = success ? "Sync completed successfully" : "Sync failed",
                        }
                    );
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during manual sync");
                    return Results.Problem("Sync failed with error: " + ex.Message);
                }
            }
        );

        // Configure graceful shutdown
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Starting MiniMed Connector Service...");

        await app.RunAsync();
    }
}
