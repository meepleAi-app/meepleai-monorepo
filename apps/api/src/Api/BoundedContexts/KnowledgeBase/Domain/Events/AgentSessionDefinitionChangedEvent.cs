using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

internal sealed class AgentSessionDefinitionChangedEvent : DomainEventBase
{
    public Guid AgentSessionId { get; }
    public Guid NewAgentDefinitionId { get; }

    public AgentSessionDefinitionChangedEvent(Guid agentSessionId, Guid newAgentDefinitionId)
    {
        AgentSessionId = agentSessionId;
        NewAgentDefinitionId = newAgentDefinitionId;
    }
}
