using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Domain event raised when an AgentDefinition is activated.
/// Issue #3808 (Epic #3687)
/// </summary>
internal sealed class AgentDefinitionActivatedEvent : DomainEventBase
{
    public Guid AgentDefinitionId { get; }

    public AgentDefinitionActivatedEvent(Guid agentDefinitionId)
    {
        AgentDefinitionId = agentDefinitionId;
    }
}
