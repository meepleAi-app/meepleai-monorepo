using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// AgentSession aggregate root linking an AgentDefinition to a specific game session with state persistence.
/// </summary>
internal sealed class AgentSession : AggregateRoot<Guid>
{
    public Guid AgentDefinitionId { get; private set; }
    public Guid GameSessionId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid GameId { get; private set; }
    public AgentConfig Config { get; private set; }
    public GameState CurrentGameState { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public bool IsActive { get; private set; }

#pragma warning disable CS8618
    private AgentSession() : base()
#pragma warning restore CS8618
    {
    }

    public AgentSession(
        Guid id,
        Guid agentDefinitionId,
        Guid gameSessionId,
        Guid userId,
        Guid gameId,
        GameState initialState,
        AgentConfig? config = null) : base(id)
    {
        if (agentDefinitionId == Guid.Empty)
            throw new ArgumentException("AgentDefinitionId cannot be empty", nameof(agentDefinitionId));
        if (gameSessionId == Guid.Empty)
            throw new ArgumentException("GameSessionId cannot be empty", nameof(gameSessionId));
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty", nameof(gameId));

        ArgumentNullException.ThrowIfNull(initialState);

        AgentDefinitionId = agentDefinitionId;
        GameSessionId = gameSessionId;
        UserId = userId;
        GameId = gameId;
        Config = config ?? AgentConfig.Default();
        CurrentGameState = initialState;
        StartedAt = DateTime.UtcNow;
        IsActive = true;

        AddDomainEvent(new AgentSessionCreatedEvent(id, agentDefinitionId, gameSessionId, userId));
    }


    public void UpdateGameState(GameState newState)
    {
        ArgumentNullException.ThrowIfNull(newState);

        if (!IsActive)
            throw new ConflictException("Cannot update state of inactive agent session");

        CurrentGameState = newState;

        AddDomainEvent(new AgentSessionStateUpdatedEvent(Id, newState.ToJson()));
    }

    public void UpdateAgentDefinition(Guid newAgentDefinitionId)
    {
        if (!IsActive)
            throw new ConflictException("Cannot update definition of inactive agent session");

        if (newAgentDefinitionId == Guid.Empty)
            throw new ArgumentException("AgentDefinitionId cannot be empty", nameof(newAgentDefinitionId));

        AgentDefinitionId = newAgentDefinitionId;

        AddDomainEvent(new AgentSessionDefinitionChangedEvent(Id, newAgentDefinitionId));
    }

    public void UpdateConfig(AgentConfig newConfig)
    {
        ArgumentNullException.ThrowIfNull(newConfig);

        if (!IsActive)
            throw new ConflictException("Cannot update config of inactive agent session");

        Config = newConfig;

        AddDomainEvent(new AgentSessionConfigUpdatedEvent(Id, newConfig.ToJson()));
    }

    public void End()
    {
        if (!IsActive)
            throw new ConflictException("Agent session is already ended");

        IsActive = false;
        EndedAt = DateTime.UtcNow;

        AddDomainEvent(new AgentSessionEndedEvent(Id, Duration));
    }

    public TimeSpan Duration =>
        EndedAt.HasValue
            ? EndedAt.Value - StartedAt
            : DateTime.UtcNow - StartedAt;
}
