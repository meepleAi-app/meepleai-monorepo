using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

internal sealed class AgentActivatedEvent : DomainEventBase
{
    public Guid AgentId { get; }

    public AgentActivatedEvent(Guid agentId)
    {
        AgentId = agentId;
    }
}
