using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

internal sealed class AgentSessionCreatedEvent : DomainEventBase
{
    public Guid AgentSessionId { get; }
    public Guid AgentDefinitionId { get; }
    public Guid GameSessionId { get; }
    public Guid UserId { get; }

    // Compatibility property — will be removed in Task 12
    public Guid AgentId => Guid.Empty;

    public AgentSessionCreatedEvent(Guid agentSessionId, Guid agentDefinitionId, Guid gameSessionId, Guid userId)
    {
        AgentSessionId = agentSessionId;
        AgentDefinitionId = agentDefinitionId;
        GameSessionId = gameSessionId;
        UserId = userId;
    }
}
