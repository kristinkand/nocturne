using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nocturne.Tools.Migration.Data;

/// <summary>
/// Design-time factory for MigrationDbContext to support Entity Framework migrations
/// </summary>
public class MigrationDbContextFactory : IDesignTimeDbContextFactory<MigrationDbContext>
{
    public MigrationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MigrationDbContext>();

        // Use a default connection string for design-time operations
        // This will be overridden at runtime with the actual connection string
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=nocturne_migration_design;Username=postgres;Password=postgres"
        );

        return new MigrationDbContext(optionsBuilder.Options);
    }
}
