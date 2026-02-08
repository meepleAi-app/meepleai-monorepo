using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Domain event raised when an AgentDefinition is created.
/// Issue #3808 (Epic #3687)
/// </summary>
internal sealed class AgentDefinitionCreatedEvent : DomainEventBase
{
    public Guid AgentDefinitionId { get; }
    public string Name { get; }

    public AgentDefinitionCreatedEvent(Guid agentDefinitionId, string name)
    {
        AgentDefinitionId = agentDefinitionId;
        Name = name;
    }
}
