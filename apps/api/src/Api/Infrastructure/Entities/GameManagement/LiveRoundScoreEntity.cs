namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// Infrastructure entity for RoundScore value object.
/// Issue #4750: Flattened table for per-round scoring in live sessions.
/// </summary>
public class LiveRoundScoreEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LiveGameSessionId { get; set; }
    public Guid PlayerId { get; set; }

    // Score Data
    public int Round { get; set; }
    public string Dimension { get; set; } = default!;
    public int Value { get; set; }
    public string? Unit { get; set; }
    public DateTime RecordedAt { get; set; }

    // Navigation Properties
    public LiveGameSessionEntity LiveGameSession { get; set; } = default!;
    public SessionPlayerEntity Player { get; set; } = default!;
}
