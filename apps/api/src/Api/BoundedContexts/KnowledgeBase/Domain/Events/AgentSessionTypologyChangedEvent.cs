using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

internal sealed class AgentSessionTypologyChangedEvent : DomainEventBase
{
    public Guid AgentSessionId { get; }
    public Guid NewTypologyId { get; }

    public AgentSessionTypologyChangedEvent(Guid agentSessionId, Guid newTypologyId)
    {
        AgentSessionId = agentSessionId;
        NewTypologyId = newTypologyId;
    }
}
