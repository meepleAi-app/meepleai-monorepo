using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Domain event raised when an AgentDefinition is updated.
/// Issue #3808 (Epic #3687)
/// </summary>
internal sealed class AgentDefinitionUpdatedEvent : DomainEventBase
{
    public Guid AgentDefinitionId { get; }
    public string ChangeDescription { get; }

    public AgentDefinitionUpdatedEvent(Guid agentDefinitionId, string changeDescription)
    {
        AgentDefinitionId = agentDefinitionId;
        ChangeDescription = changeDescription;
    }
}
