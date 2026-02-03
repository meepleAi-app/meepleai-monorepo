using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// AgentSession aggregate root linking an agent to a specific game session with state persistence.
/// </summary>
/// <remarks>
/// Blocked by: GST #3160 (game_sessions table dependency).
/// This entity enables session-based agent state tracking for multi-turn game assistance.
/// </remarks>
internal sealed class AgentSession : AggregateRoot<Guid>
{
    public Guid AgentId { get; private set; }
    public Guid GameSessionId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid GameId { get; private set; }
    public Guid TypologyId { get; private set; }
    public AgentConfig Config { get; private set; } // Issue #3253
    public GameState CurrentGameState { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private AgentSession() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new agent session linked to a game session.
    /// </summary>
    public AgentSession(
        Guid id,
        Guid agentId,
        Guid gameSessionId,
        Guid userId,
        Guid gameId,
        Guid typologyId,
        GameState initialState,
        AgentConfig? config = null) : base(id)
    {
        if (agentId == Guid.Empty)
            throw new ArgumentException("AgentId cannot be empty", nameof(agentId));
        if (gameSessionId == Guid.Empty)
            throw new ArgumentException("GameSessionId cannot be empty", nameof(gameSessionId));
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty", nameof(gameId));
        if (typologyId == Guid.Empty)
            throw new ArgumentException("TypologyId cannot be empty", nameof(typologyId));

        ArgumentNullException.ThrowIfNull(initialState);

        AgentId = agentId;
        GameSessionId = gameSessionId;
        UserId = userId;
        GameId = gameId;
        TypologyId = typologyId;
        Config = config ?? AgentConfig.Default(); // Issue #3253
        CurrentGameState = initialState;
        StartedAt = DateTime.UtcNow;
        IsActive = true;

        AddDomainEvent(new AgentSessionCreatedEvent(id, agentId, gameSessionId, userId));
    }

    /// <summary>
    /// Updates the current game state.
    /// </summary>
    public void UpdateGameState(GameState newState)
    {
        ArgumentNullException.ThrowIfNull(newState);

        if (!IsActive)
            throw new ConflictException("Cannot update state of inactive agent session");

        CurrentGameState = newState;

        AddDomainEvent(new AgentSessionStateUpdatedEvent(Id, newState.ToJson()));
    }

    /// <summary>
    /// Updates the agent typology during an active session.
    /// Issue #3252 (BACK-AGT-001).
    /// </summary>
    public void UpdateTypology(Guid newTypologyId)
    {
        if (!IsActive)
            throw new ConflictException("Cannot update typology of inactive agent session");

        if (newTypologyId == Guid.Empty)
            throw new ArgumentException("TypologyId cannot be empty", nameof(newTypologyId));

        TypologyId = newTypologyId;

        AddDomainEvent(new AgentSessionTypologyChangedEvent(Id, newTypologyId));
    }

    /// <summary>
    /// Updates the agent runtime configuration during an active session.
    /// Issue #3253 (BACK-AGT-002).
    /// </summary>
    public void UpdateConfig(AgentConfig newConfig)
    {
        ArgumentNullException.ThrowIfNull(newConfig);

        if (!IsActive)
            throw new ConflictException("Cannot update config of inactive agent session");

        Config = newConfig;

        AddDomainEvent(new AgentSessionConfigUpdatedEvent(Id, newConfig.ToJson()));
    }

    /// <summary>
    /// Ends the agent session.
    /// </summary>
    public void End()
    {
        if (!IsActive)
            throw new ConflictException("Agent session is already ended");

        IsActive = false;
        EndedAt = DateTime.UtcNow;

        AddDomainEvent(new AgentSessionEndedEvent(Id, Duration));
    }

    /// <summary>
    /// Gets the duration of the agent session.
    /// </summary>
    public TimeSpan Duration =>
        EndedAt.HasValue
            ? EndedAt.Value - StartedAt
            : DateTime.UtcNow - StartedAt;
}