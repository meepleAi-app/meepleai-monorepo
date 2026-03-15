using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Entities.PauseSnapshot;

/// <summary>
/// Full-state snapshot captured when a live game session is paused.
/// Distinct from the delta-based <c>SessionSnapshot</c> (which stores incremental patches).
/// PauseSnapshot stores a complete picture of session state for reliable save/resume.
/// </summary>
#pragma warning disable MA0049
public sealed class PauseSnapshot : Entity<Guid>
#pragma warning restore MA0049
{
    public Guid LiveGameSessionId { get; private set; }
    public int CurrentTurn { get; private set; }
    public string? CurrentPhase { get; private set; }
    public List<PlayerScoreSnapshot> PlayerScores { get; private set; } = new();
    public List<Guid> AttachmentIds { get; private set; } = new();
    public List<RuleDisputeEntry> Disputes { get; private set; } = new();
    public string? AgentConversationSummary { get; private set; }
    public string? GameStateJson { get; private set; }
    public DateTime SavedAt { get; private set; }
    public Guid SavedByUserId { get; private set; }
    public bool IsAutoSave { get; private set; }

    // EF Core parameterless constructor
    private PauseSnapshot() { }

    /// <summary>
    /// Creates a new PauseSnapshot capturing full session state at the time of pause.
    /// </summary>
    public static PauseSnapshot Create(
        Guid liveGameSessionId,
        int currentTurn,
        string? currentPhase,
        List<PlayerScoreSnapshot> playerScores,
        Guid savedByUserId,
        bool isAutoSave,
        List<Guid>? attachmentIds = null,
        List<RuleDisputeEntry>? disputes = null,
        string? gameStateJson = null)
    {
        if (liveGameSessionId == Guid.Empty)
            throw new ArgumentException("LiveGameSessionId cannot be empty.", nameof(liveGameSessionId));
        if (currentTurn < 0)
            throw new ArgumentOutOfRangeException(nameof(currentTurn), "CurrentTurn cannot be negative.");
        if (savedByUserId == Guid.Empty)
            throw new ArgumentException("SavedByUserId cannot be empty.", nameof(savedByUserId));

        return new PauseSnapshot
        {
            Id = Guid.NewGuid(),
            LiveGameSessionId = liveGameSessionId,
            CurrentTurn = currentTurn,
            CurrentPhase = currentPhase,
            PlayerScores = playerScores ?? new List<PlayerScoreSnapshot>(),
            AttachmentIds = attachmentIds ?? new List<Guid>(),
            Disputes = disputes ?? new List<RuleDisputeEntry>(),
            GameStateJson = gameStateJson,
            SavedAt = DateTime.UtcNow,
            SavedByUserId = savedByUserId,
            IsAutoSave = isAutoSave
        };
    }

    /// <summary>
    /// Sets the agent conversation summary after the AI summarises the session context.
    /// Called asynchronously once the agent produces a summary post-pause.
    /// </summary>
    public void UpdateSummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            throw new ArgumentException("Summary cannot be empty.", nameof(summary));

        AgentConversationSummary = summary;
    }
}
