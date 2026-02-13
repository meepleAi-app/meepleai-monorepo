using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.UserLibrary.Domain.Events;

/// <summary>
/// Domain event raised when an AI agent is linked to a private game.
/// Issue #4228: SharedGame and PrivateGame → AgentDefinition relationship
/// </summary>
internal sealed class AgentLinkedToPrivateGameEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the private game.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// Gets the ID of the linked agent definition.
    /// </summary>
    public Guid AgentDefinitionId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentLinkedToPrivateGameEvent"/> class.
    /// </summary>
    public AgentLinkedToPrivateGameEvent(Guid gameId, Guid agentDefinitionId)
    {
        GameId = gameId;
        AgentDefinitionId = agentDefinitionId;
    }
}
