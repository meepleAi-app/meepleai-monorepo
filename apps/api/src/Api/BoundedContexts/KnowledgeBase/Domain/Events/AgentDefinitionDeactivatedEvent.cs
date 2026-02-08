using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Domain event raised when an AgentDefinition is deactivated.
/// Issue #3808 (Epic #3687)
/// </summary>
internal sealed class AgentDefinitionDeactivatedEvent : DomainEventBase
{
    public Guid AgentDefinitionId { get; }

    public AgentDefinitionDeactivatedEvent(Guid agentDefinitionId)
    {
        AgentDefinitionId = agentDefinitionId;
    }
}
