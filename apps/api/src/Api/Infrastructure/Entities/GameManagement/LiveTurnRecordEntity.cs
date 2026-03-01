namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// Infrastructure entity for TurnRecord value object.
/// Issue #4750: Flattened table for turn history in live sessions.
/// </summary>
public class LiveTurnRecordEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LiveGameSessionId { get; set; }

    // Turn Data
    public int TurnIndex { get; set; }
    public Guid PlayerId { get; set; }
    public int? PhaseIndex { get; set; }
    public string? PhaseName { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    // Navigation Properties
    public LiveGameSessionEntity LiveGameSession { get; set; } = default!;
    public SessionPlayerEntity Player { get; set; } = default!;
}
