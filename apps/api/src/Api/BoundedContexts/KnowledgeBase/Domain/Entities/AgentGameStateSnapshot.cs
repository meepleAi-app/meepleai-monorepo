using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Represents a game state snapshot for position similarity context retrieval.
/// Issue #3493: PostgreSQL Schema Extensions for Multi-Agent System.
/// </summary>
/// <remarks>
/// Stores board state JSON with embeddings for similar position search
/// to provide contextual game state information for AI agents.
/// </remarks>
internal sealed class AgentGameStateSnapshot : Entity<Guid>
{
    public Guid GameId { get; private set; }
    public Guid AgentSessionId { get; private set; }
    public string BoardStateJson { get; private set; }
    public int TurnNumber { get; private set; }
    public Guid? ActivePlayerId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Vector? Embedding { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private AgentGameStateSnapshot() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new game state snapshot.
    /// </summary>
    public AgentGameStateSnapshot(
        Guid id,
        Guid gameId,
        Guid agentSessionId,
        string boardStateJson,
        int turnNumber,
        Guid? activePlayerId = null,
        Vector? embedding = null) : base(id)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("Game ID cannot be empty", nameof(gameId));
        if (agentSessionId == Guid.Empty)
            throw new ArgumentException("Agent session ID cannot be empty", nameof(agentSessionId));
        if (string.IsNullOrWhiteSpace(boardStateJson))
            throw new ArgumentException("Board state JSON cannot be empty", nameof(boardStateJson));
        if (turnNumber < 0)
            throw new ArgumentException("Turn number cannot be negative", nameof(turnNumber));

        GameId = gameId;
        AgentSessionId = agentSessionId;
        BoardStateJson = boardStateJson;
        TurnNumber = turnNumber;
        ActivePlayerId = activePlayerId;
        CreatedAt = DateTime.UtcNow;
        Embedding = embedding;
    }

    /// <summary>
    /// Updates the embedding for this snapshot.
    /// </summary>
    public void SetEmbedding(Vector embedding)
    {
        Embedding = embedding ?? throw new ArgumentNullException(nameof(embedding));
    }

    /// <summary>
    /// Updates the board state.
    /// </summary>
    public void UpdateBoardState(string boardStateJson, int turnNumber, Guid? activePlayerId = null)
    {
        if (string.IsNullOrWhiteSpace(boardStateJson))
            throw new ArgumentException("Board state JSON cannot be empty", nameof(boardStateJson));
        if (turnNumber < 0)
            throw new ArgumentException("Turn number cannot be negative", nameof(turnNumber));

        BoardStateJson = boardStateJson;
        TurnNumber = turnNumber;
        ActivePlayerId = activePlayerId;
    }
}
