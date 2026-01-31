using System.ComponentModel.DataAnnotations;

namespace Api.Infrastructure.Entities.SessionTracking;

/// <summary>
/// Persistence entity for Session (EF Core mapping).
/// Maps to session_tracking_sessions table.
/// </summary>
public class SessionEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? GameId { get; set; }

    [MaxLength(6)]
    public string SessionCode { get; set; } = string.Empty;

    [MaxLength(20)]
    public string SessionType { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    public DateTime SessionDate { get; set; }

    [MaxLength(100)]
    public string? Location { get; set; }

    public DateTime? FinalizedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    // Navigation properties
    public ICollection<ParticipantEntity> Participants { get; set; } = new List<ParticipantEntity>();
}
