using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Domain event raised when an AI agent is linked to a shared game.
/// Issue #4228: SharedGame and PrivateGame → AgentDefinition relationship
/// </summary>
internal sealed class AgentLinkedToSharedGameEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the shared game.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// Gets the ID of the linked agent definition.
    /// </summary>
    public Guid AgentDefinitionId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentLinkedToSharedGameEvent"/> class.
    /// </summary>
    public AgentLinkedToSharedGameEvent(Guid gameId, Guid agentDefinitionId)
    {
        GameId = gameId;
        AgentDefinitionId = agentDefinitionId;
    }
}
