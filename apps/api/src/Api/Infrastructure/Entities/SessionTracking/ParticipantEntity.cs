using System.ComponentModel.DataAnnotations;

namespace Api.Infrastructure.Entities.SessionTracking;

/// <summary>
/// Persistence entity for Participant (EF Core mapping).
/// Maps to session_tracking_participants table.
/// </summary>
public class ParticipantEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid? UserId { get; set; }

    [MaxLength(50)]
    public string DisplayName { get; set; } = string.Empty;

    public bool IsOwner { get; set; }
    public int JoinOrder { get; set; }
    public int? FinalRank { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation property
    public SessionEntity? Session { get; set; }
}
