using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.UserNotifications;

/// <summary>
/// EF Core entity for email templates with versioning support.
/// Persistence model for EmailTemplate aggregate.
/// Issue #52: P4.1 Domain entity for admin email template management.
/// </summary>
[Table("email_templates")]
public class EmailTemplateEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    [Column("locale")]
    public string Locale { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    [Column("subject")]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [Column("html_body", TypeName = "text")]
    public string HtmlBody { get; set; } = string.Empty;

    [Required]
    [Column("version")]
    public int Version { get; set; } = 1;

    [Required]
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("last_modified_by")]
    public Guid? LastModifiedBy { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
