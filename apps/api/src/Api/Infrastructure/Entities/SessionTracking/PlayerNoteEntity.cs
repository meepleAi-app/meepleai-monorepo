using System.ComponentModel.DataAnnotations;

namespace Api.Infrastructure.Entities.SessionTracking;

/// <summary>
/// Persistence entity for PlayerNote (EF Core mapping).
/// Maps to session_tracking_notes table.
/// </summary>
public class PlayerNoteEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid ParticipantId { get; set; }

    [MaxLength(20)]
    public string NoteType { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? TemplateKey { get; set; }

    public string Content { get; set; } = string.Empty;
    public bool IsHidden { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public SessionEntity? Session { get; set; }
    public ParticipantEntity? Participant { get; set; }
}
