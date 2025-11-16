using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nocturne.Tools.Migration.Commands;
using Nocturne.Tools.Migration.Data;
using Nocturne.Tools.Migration.Infrastructure;
using Nocturne.Tools.Migration.Services;
using Spectre.Console.Cli;

var services = new ServiceCollection();

// Add configuration
// Find the root directory by looking for the .git directory or solution file
var currentDir = Directory.GetCurrentDirectory();
var rootPath = currentDir;
while (
    rootPath != null
    && !Directory.Exists(Path.Combine(rootPath, ".git"))
    && !File.Exists(Path.Combine(rootPath, "*.sln"))
)
{
    rootPath = Directory.GetParent(rootPath)?.FullName;
}
rootPath ??= currentDir; // fallback to current directory

Console.WriteLine($"Root path found: {rootPath}");
Console.WriteLine($"Looking for appsettings.json at: {Path.Combine(rootPath, "appsettings.json")}");
Console.WriteLine($"File exists: {File.Exists(Path.Combine(rootPath, "appsettings.json"))}");

var configuration = new ConfigurationBuilder()
    .SetBasePath(rootPath)
    .AddJsonFile(Path.Combine(rootPath, "appsettings.json"), optional: false, reloadOnChange: true)
    .AddJsonFile(
        Path.Combine(
            rootPath,
            $"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development"}.json"
        ),
        optional: true
    )
    .AddEnvironmentVariables()
    .Build();

// Add services
services.AddSingleton<IConfiguration>(configuration);
services.AddLogging(builder =>
{
    builder.ClearProviders();
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});
services.AddScoped<IMigrationEngine, MigrationEngine>();
services.AddScoped<IBackupService, BackupService>();
services.AddScoped<IRollbackService, RollbackService>();
services.AddScoped<IRecoveryService, RecoveryService>();
services.AddScoped<
    Nocturne.Tools.Migration.Services.IDatabaseSchemaIntrospectionService,
    Nocturne.Tools.Migration.Services.DatabaseSchemaIntrospectionService
>();

// Use the local SchemaValidationService which fully implements IValidationService for migrations
services.AddScoped<
    Nocturne.Tools.Abstractions.Services.IValidationService,
    Nocturne.Tools.Migration.Services.SchemaValidationService
>();

// Add database connection service
services.AddScoped<IDatabaseConnectionService, DatabaseConnectionService>();

// Note: DbContext will be configured per command with the provided connection string
// This is just a placeholder registration - actual connection string is set in each command
services.AddDbContext<MigrationDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("nocturne-postgres"))
);

// Create the command app
var app = new CommandApp(new TypeRegistrar(services));

// Configure commands
app.Configure(config =>
{
    config
        .AddCommand<MigrateCommand>("migrate")
        .WithDescription("Migrate data from MongoDB to PostgreSQL using the migration engine");

    config.AddCommand<RollbackCommand>("rollback").WithDescription("Rollback a previous migration");

    config.AddCommand<BackupCommand>("backup").WithDescription("Create a backup of the database");

    config
        .AddCommand<RecoveryCommand>("recovery")
        .WithDescription("Recover from a backup or failed migration");

    config
        .AddCommand<TestConnectionsCommand>("test-connections")
        .WithDescription("Test database connections before migration");
});

return await app.RunAsync(args);
