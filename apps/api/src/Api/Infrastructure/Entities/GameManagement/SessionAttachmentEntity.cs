using System.ComponentModel.DataAnnotations;

namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// EF Core persistence entity for session photo attachments.
/// Issue #5360 - SessionAttachment EF Core configuration + migration.
/// </summary>
public class SessionAttachmentEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public int? SnapshotIndex { get; set; }
    public Guid PlayerId { get; set; }
    public int AttachmentType { get; set; }

    [MaxLength(2048)]
    public string BlobUrl { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string? ThumbnailUrl { get; set; }

    [MaxLength(200)]
    public string? Caption { get; set; }

    [MaxLength(50)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
