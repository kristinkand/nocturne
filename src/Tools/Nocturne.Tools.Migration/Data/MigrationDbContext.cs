using Microsoft.EntityFrameworkCore;
using Nocturne.Tools.Migration.Models;

namespace Nocturne.Tools.Migration.Data;

/// <summary>
/// Entity Framework DbContext for migration tracking tables only
/// This context manages only migration-specific tracking tables and does NOT include main application entities
/// Main application data access should use MigrationDataContext
/// </summary>
public class MigrationDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the MigrationDbContext class
    /// </summary>
    /// <param name="options">The options for this context</param>
    public MigrationDbContext(DbContextOptions<MigrationDbContext> options)
        : base(options) { }

    // Migration tracking tables - these are tool-specific and don't belong in the main application
    public DbSet<MigrationCheckpoint> MigrationCheckpoints { get; set; }
    public DbSet<MigrationLog> MigrationLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure migration-specific tables only
        modelBuilder.Entity<MigrationCheckpoint>().ToTable("migration_checkpoints");
        modelBuilder.Entity<MigrationLog>().ToTable("migration_logs");

        // Configure migration tracking indexes for performance
        ConfigureMigrationIndexes(modelBuilder);
    }

    private static void ConfigureMigrationIndexes(ModelBuilder modelBuilder)
    {
        // Migration tracking indexes
        modelBuilder
            .Entity<MigrationCheckpoint>()
            .HasIndex(c => c.MigrationId)
            .HasDatabaseName("ix_migration_checkpoints_migration_id");

        modelBuilder
            .Entity<MigrationLog>()
            .HasIndex(l => new { l.MigrationId, l.Timestamp })
            .HasDatabaseName("ix_migration_logs_migration_timestamp");
    }
}
