using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.Entities.Administration;

/// <summary>
/// EF Core entity for tracking database size metrics over time.
/// </summary>
[Table("database_metrics_snapshots")]
[Index(nameof(RecordedAt))]
public class DatabaseMetricsSnapshotEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("recorded_at")]
    public DateTime RecordedAt { get; set; }

    [Required]
    [Column("total_size_bytes")]
    public long TotalSizeBytes { get; set; }

    [Required]
    [Column("table_count")]
    public int TableCount { get; set; }

    [Required]
    [Column("index_size_bytes")]
    public long IndexSizeBytes { get; set; }

    [Required]
    [Column("active_connections")]
    public int ActiveConnections { get; set; }
}
