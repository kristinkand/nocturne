using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nocturne.API.Configuration;
using Nocturne.API.Tests.Integration.Infrastructure;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Extensions;
using Xunit;

namespace Nocturne.API.Tests.Integration;

public class CustomWebApplicationFactory
    : WebApplicationFactory<Program>,
        IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _databaseFixture;

    public CustomWebApplicationFactory()
    {
        _databaseFixture = new TestDatabaseFixture();
    }

    public TestDatabaseFixture DatabaseFixture => _databaseFixture;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(
            (context, config) =>
            {
                // Clear existing configuration
                config.Sources.Clear();

                // Add test configuration
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        // PostgreSQL database configuration (primary)
                        ["ConnectionStrings:DefaultConnection"] =
                            _databaseFixture.PostgreSqlConnectionString,
                        ["PostgreSql:ConnectionString"] =
                            _databaseFixture.PostgreSqlConnectionString,
                        ["PostgreSql:DatabaseName"] = "nocturne_test",

                        // MongoDB configuration (backward compatibility during migration)
                        ["MongoDB:ConnectionString"] = "mongodb://localhost:27017",
                        ["MongoDB:DatabaseName"] = "nocturne_test",

                        // API configuration
                        ["API_SECRET"] = "test-secret-for-integration-tests",
                        ["NIGHTSCOUT_API_SECRET"] = "test-secret-for-integration-tests",

                        // Disable external services for testing
                        ["Features:EnableExternalConnectors"] = "false",
                        ["Features:EnableRealTimeNotifications"] = "true",

                        // Test environment settings
                        ["Environment"] = "Testing",
                        ["Logging:LogLevel:Default"] = "Warning",
                        ["Logging:LogLevel:Nocturne"] = "Information",

                        // Disable authentication for certain tests
                        ["Authentication:RequireApiSecret"] = "false",
                    }
                );
            }
        );

        builder.ConfigureServices(services =>
        {
            // Configure test-specific services
            ConfigureTestServices(services);
        });

        builder.UseEnvironment("Testing");

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Warning);
            logging.AddFilter("Nocturne", LogLevel.Information);
        });
    }

    private void ConfigureTestServices(IServiceCollection services)
    {
        // Remove existing PostgreSQL infrastructure services if registered
        var descriptorsToRemove = services
            .Where(d =>
                d.ServiceType == typeof(DbContextOptions<NocturneDbContext>)
                || d.ServiceType == typeof(NocturneDbContext)
                || d.ServiceType.Name.Contains("PostgreSql")
                || d.ServiceType.Name.Contains("MongoDb")
            )
            .ToList();

        foreach (var descriptor in descriptorsToRemove)
        {
            services.Remove(descriptor);
        }

        // Configure PostgreSQL infrastructure for testing using the extension method
        // This ensures all necessary services are registered properly
        services.AddPostgreSqlInfrastructure(
            _databaseFixture.PostgreSqlConnectionString,
            config =>
            {
                config.EnableDetailedErrors = true;
                config.EnableSensitiveDataLogging = true;
            }
        );

        // Override MongoDB configuration (for backward compatibility)
        // Commented out - MongoDbConfiguration class doesn't exist
        // services.Configure<MongoDbConfiguration>(options =>
        // {
        //     options.ConnectionString = "mongodb://localhost:27017";
        //     options.DatabaseName = "nocturne_test";
        // });

        // Add test-specific service overrides if needed
        // services.AddSingleton<ITestService, TestServiceImplementation>();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Let xUnit handle the test fixture disposal
            // No need to dispose manually here as it's handled by the test framework
        }
        base.Dispose(disposing);
    }

    public async ValueTask InitializeAsync()
    {
        await _databaseFixture.InitializeAsync();
    }

    public async Task CleanupAsync()
    {
        await _databaseFixture.CleanupAsync();
    }
}
