namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// EF Core persistence entity for PauseSnapshot.
/// Stores a full-state snapshot when a live game session is paused.
/// PlayerScores, AttachmentIds and Disputes are persisted as JSONB.
/// </summary>
public class PauseSnapshotEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LiveGameSessionId { get; set; }
    public int CurrentTurn { get; set; }

    /// <summary>Max 100 characters — phase name within the game (e.g. "Setup", "Round 3").</summary>
    public string? CurrentPhase { get; set; }

    /// <summary>JSONB: serialised List&lt;PlayerScoreSnapshot&gt;.</summary>
    public string PlayerScoresJson { get; set; } = "[]";

    /// <summary>JSONB: serialised List&lt;Guid&gt; of session attachment IDs.</summary>
    public string AttachmentIdsJson { get; set; } = "[]";

    /// <summary>JSONB: serialised List&lt;RuleDisputeEntry&gt;.</summary>
    public string DisputesJson { get; set; } = "[]";

    public string? AgentConversationSummary { get; set; }

    /// <summary>JSONB: free-form game state blob.</summary>
    public string? GameStateJson { get; set; }

    public DateTime SavedAt { get; set; }
    public Guid SavedByUserId { get; set; }
    public bool IsAutoSave { get; set; }
}
