using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities;

/// <summary>
/// EF Core entity for admin_reports table
/// ISSUE-916: Report definitions storage
/// </summary>
[Table("admin_reports")]
internal sealed class AdminReportEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("name")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("description")]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Column("template")]
    public int Template { get; set; }  // ReportTemplate enum

    [Required]
    [Column("format")]
    public int Format { get; set; }  // ReportFormat enum

    [Required]
    [Column("parameters", TypeName = "jsonb")]
    public string ParametersJson { get; set; } = "{}";

    [Column("schedule_expression")]
    [MaxLength(100)]
    public string? ScheduleExpression { get; set; }

    [Required]
    [Column("is_active")]
    public bool IsActive { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("last_executed_at")]
    public DateTime? LastExecutedAt { get; set; }

    [Required]
    [Column("created_by")]
    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [Required]
    [Column("email_recipients", TypeName = "jsonb")]
    public string EmailRecipientsJson { get; set; } = "[]"; // ISSUE-918: Email delivery integration

    // Navigation property
    public ICollection<ReportExecutionEntity> Executions { get; set; } = new List<ReportExecutionEntity>();
}
