using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Tools.Migration.Models;

/// <summary>
/// Migration log entry
/// </summary>
[Table("migration_logs")]
public class MigrationLog
{
    /// <summary>
    /// Primary key
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Migration identifier
    /// </summary>
    [Column("migration_id")]
    [MaxLength(255)]
    public required string MigrationId { get; set; }

    /// <summary>
    /// Log level
    /// </summary>
    [Column("level")]
    [MaxLength(50)]
    public required string Level { get; set; }

    /// <summary>
    /// Log message
    /// </summary>
    [Column("message")]
    public required string Message { get; set; }

    /// <summary>
    /// Exception details if any
    /// </summary>
    [Column("exception")]
    public string? Exception { get; set; }

    /// <summary>
    /// Log timestamp
    /// </summary>
    [Column("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional log data as JSONB
    /// </summary>
    [Column("metadata", TypeName = "jsonb")]
    public string? Metadata { get; set; }
}
