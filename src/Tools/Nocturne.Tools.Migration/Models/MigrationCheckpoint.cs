using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Tools.Migration.Models;

/// <summary>
/// Migration checkpoint for resume functionality
/// </summary>
[Table("migration_checkpoints")]
public class MigrationCheckpoint
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
    /// Collection being migrated
    /// </summary>
    [Column("collection_name")]
    [MaxLength(255)]
    public required string CollectionName { get; set; }

    /// <summary>
    /// Last processed document ID
    /// </summary>
    [Column("last_processed_id")]
    [MaxLength(255)]
    public string? LastProcessedId { get; set; }

    /// <summary>
    /// Number of documents processed
    /// </summary>
    [Column("documents_processed")]
    public long DocumentsProcessed { get; set; }

    /// <summary>
    /// Total number of documents to process
    /// </summary>
    [Column("total_documents")]
    public long TotalDocuments { get; set; }

    /// <summary>
    /// Migration start time
    /// </summary>
    [Column("start_time")]
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Last update time
    /// </summary>
    [Column("last_update")]
    public DateTime LastUpdate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Migration status
    /// </summary>
    [Column("status")]
    [MaxLength(50)]
    public required string Status { get; set; }

    /// <summary>
    /// Additional checkpoint data as JSONB
    /// </summary>
    [Column("checkpoint_data", TypeName = "jsonb")]
    public string? CheckpointData { get; set; }
}
