using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

internal sealed class AgentSessionStateUpdatedEvent : DomainEventBase
{
    public Guid AgentSessionId { get; }
    public string NewStateJson { get; }

    public AgentSessionStateUpdatedEvent(Guid agentSessionId, string newStateJson)
    {
        AgentSessionId = agentSessionId;
        NewStateJson = newStateJson;
    }
}
