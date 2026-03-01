using Api.Infrastructure.Entities.Authentication;

namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// Infrastructure entity for LiveSessionPlayer.
/// Issue #4750: Maps domain LiveSessionPlayer to database table.
/// </summary>
public class SessionPlayerEntity
{
    public Guid Id { get; set; }
    public Guid LiveGameSessionId { get; set; }

    // Player Identity
    public Guid? UserId { get; set; }
    public string DisplayName { get; set; } = default!;
    public string? AvatarUrl { get; set; }

    // Session Role & Appearance
    public string Color { get; set; } = default!; // PlayerColor enum stored as string
    public string Role { get; set; } = default!; // PlayerRole enum stored as string

    // Team Assignment (Optional)
    public Guid? TeamId { get; set; }

    // Scoring
    public int TotalScore { get; set; }
    public int CurrentRank { get; set; }

    // Timestamps
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; }

    // Navigation Properties
    public LiveGameSessionEntity LiveGameSession { get; set; } = default!;
    public UserEntity? User { get; set; }
    public SessionTeamEntity? Team { get; set; }
}
