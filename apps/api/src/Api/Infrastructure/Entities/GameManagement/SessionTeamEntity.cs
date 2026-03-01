namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// Infrastructure entity for LiveSessionTeam.
/// Issue #4750: Maps domain LiveSessionTeam to database table.
/// </summary>
public class SessionTeamEntity
{
    public Guid Id { get; set; }
    public Guid LiveGameSessionId { get; set; }

    // Team Identity
    public string Name { get; set; } = default!;
    public string Color { get; set; } = default!; // #RRGGBB hex color

    // Scoring
    public int TeamScore { get; set; }
    public int CurrentRank { get; set; }

    // Navigation Properties
    public LiveGameSessionEntity LiveGameSession { get; set; } = default!;
    public ICollection<SessionPlayerEntity> Players { get; set; } = new List<SessionPlayerEntity>();
}
