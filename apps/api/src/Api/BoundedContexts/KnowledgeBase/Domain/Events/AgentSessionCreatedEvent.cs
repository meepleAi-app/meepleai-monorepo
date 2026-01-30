using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

internal sealed class AgentSessionCreatedEvent : DomainEventBase
{
    public Guid AgentSessionId { get; }
    public Guid AgentId { get; }
    public Guid GameSessionId { get; }
    public Guid UserId { get; }

    public AgentSessionCreatedEvent(Guid agentSessionId, Guid agentId, Guid gameSessionId, Guid userId)
    {
        AgentSessionId = agentSessionId;
        AgentId = agentId;
        GameSessionId = gameSessionId;
        UserId = userId;
    }
}
