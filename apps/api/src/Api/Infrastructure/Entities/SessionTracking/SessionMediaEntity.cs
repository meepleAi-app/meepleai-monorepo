using System.ComponentModel.DataAnnotations;

namespace Api.Infrastructure.Entities.SessionTracking;

/// <summary>
/// Persistence entity for SessionMedia (EF Core mapping).
/// Maps to session_tracking_media table.
/// Issue #4760
/// </summary>
public class SessionMediaEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid? SnapshotId { get; set; }

    [MaxLength(500)]
    public string FileId { get; set; } = string.Empty;

    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    [MaxLength(20)]
    public string MediaType { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Caption { get; set; }

    [MaxLength(500)]
    public string? ThumbnailFileId { get; set; }

    public int? TurnNumber { get; set; }
    public bool IsSharedWithSession { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public SessionEntity? Session { get; set; }
    public ParticipantEntity? Participant { get; set; }
}
