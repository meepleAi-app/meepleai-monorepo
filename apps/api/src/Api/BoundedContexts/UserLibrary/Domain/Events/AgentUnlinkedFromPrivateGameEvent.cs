using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.UserLibrary.Domain.Events;

/// <summary>
/// Domain event raised when an AI agent is unlinked from a private game.
/// Issue #4228: SharedGame and PrivateGame → AgentDefinition relationship
/// </summary>
internal sealed class AgentUnlinkedFromPrivateGameEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the private game.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// Gets the ID of the agent definition that was unlinked.
    /// </summary>
    public Guid AgentDefinitionId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentUnlinkedFromPrivateGameEvent"/> class.
    /// </summary>
    public AgentUnlinkedFromPrivateGameEvent(Guid gameId, Guid agentDefinitionId)
    {
        GameId = gameId;
        AgentDefinitionId = agentDefinitionId;
    }
}
