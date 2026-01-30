using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

internal sealed class AgentSessionEndedEvent : DomainEventBase
{
    public Guid AgentSessionId { get; }
    public TimeSpan Duration { get; }

    public AgentSessionEndedEvent(Guid agentSessionId, TimeSpan duration)
    {
        AgentSessionId = agentSessionId;
        Duration = duration;
    }
}
