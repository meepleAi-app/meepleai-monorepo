using Api.Infrastructure.Entities.Authentication;

namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// Infrastructure entity for SessionParticipant.
/// Maps domain SessionParticipant to database table for guest and registered user participants.
/// </summary>
public class SessionParticipantEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }

    // Identity - one of UserId or GuestName will be set
    public Guid? UserId { get; set; }
    public string? GuestName { get; set; }

    // Role & Access
    public string Role { get; set; } = default!; // ParticipantRole enum stored as string
    public bool AgentAccessEnabled { get; set; }
    public string ConnectionToken { get; set; } = default!; // 6-char PIN

    // Timestamps
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }

    // Navigation Properties
    public LiveGameSessionEntity LiveGameSession { get; set; } = default!;
    public UserEntity? User { get; set; }
}
