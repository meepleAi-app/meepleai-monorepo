using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

internal sealed class AgentDeactivatedEvent : DomainEventBase
{
    public Guid AgentId { get; }

    public AgentDeactivatedEvent(Guid agentId)
    {
        AgentId = agentId;
    }
}
