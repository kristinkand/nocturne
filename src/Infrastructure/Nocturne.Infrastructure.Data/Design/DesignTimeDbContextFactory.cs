using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Nocturne.Infrastructure.Data.Design;

/// <summary>
/// Design-time factory for NocturneDbContext to support Entity Framework migrations
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NocturneDbContext>
{
    /// <summary>
    /// Creates a new instance of NocturneDbContext for design-time operations
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>A configured NocturneDbContext instance</returns>
    public NocturneDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NocturneDbContext>();

        // Resolve connection string from environment or appsettings.json, with a sensible default
        string? connectionString =
            // Prefer connection strings provided via environment variables
            Environment.GetEnvironmentVariable("ConnectionStrings__nocturne-postgres")
            ?? Environment.GetEnvironmentVariable("PostgreSql__ConnectionString")
            ??
            // Try load appsettings.json from current working directory
            TryGetConnectionStringFromConfig(Directory.GetCurrentDirectory(), "nocturne-postgres")
            ?? TryGetPostgreSqlFromConfig(Directory.GetCurrentDirectory())
            ??
            // Fallback to Docker Compose defaults exposed on localhost
            "Host=localhost;Port=5432;Database=nocturne;Username=nocturne_user;Password=nocturne_password";

        optionsBuilder.UseNpgsql(connectionString);

        return new NocturneDbContext(optionsBuilder.Options);
    }

    private static string? TryGetConnectionStringFromConfig(string basePath, string name)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        var config = builder.Build();
        return config.GetConnectionString(name);
    }

    private static string? TryGetPostgreSqlFromConfig(string basePath)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        var config = builder.Build();
        return config.GetSection("PostgreSql:ConnectionString").Value;
    }
}
