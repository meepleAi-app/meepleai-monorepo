using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.SessionTracking;

/// <summary>
/// Persistence entity for SessionNote.
/// </summary>
[Table("SessionNotes")]
public class SessionNoteEntity
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid SessionId { get; set; }

    [Required]
    public Guid ParticipantId { get; set; }

    /// <summary>
    /// The encrypted note content.
    /// </summary>
    [Required]
    public string EncryptedContent { get; set; } = string.Empty;

    /// <summary>
    /// Whether the note is revealed to all participants.
    /// </summary>
    public bool IsRevealed { get; set; }

    /// <summary>
    /// Optional text to show when the note is obscured.
    /// </summary>
    [MaxLength(500)]
    public string? ObscuredText { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(SessionId))]
    public virtual GameSessionEntity? Session { get; set; }
}
