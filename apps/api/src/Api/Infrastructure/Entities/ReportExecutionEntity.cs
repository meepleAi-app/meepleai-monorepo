using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities;

/// <summary>
/// EF Core entity for admin_report_executions table
/// ISSUE-916: Report execution history storage
/// </summary>
[Table("admin_report_executions")]
internal sealed class ReportExecutionEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("report_id")]
    public Guid ReportId { get; set; }

    [Required]
    [Column("started_at")]
    public DateTime StartedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [Required]
    [Column("status")]
    public int Status { get; set; }  // ReportExecutionStatus enum

    [Column("error_message")]
    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    [Column("output_path")]
    [MaxLength(500)]
    public string? OutputPath { get; set; }

    [Column("file_size_bytes")]
    public long? FileSizeBytes { get; set; }

    [Column("duration_ms")]
    public long? DurationMs { get; set; }

    [Required]
    [Column("execution_metadata", TypeName = "jsonb")]
    public string ExecutionMetadataJson { get; set; } = "{}";

    // Navigation property
    [ForeignKey("ReportId")]
    public AdminReportEntity? Report { get; set; }
}
