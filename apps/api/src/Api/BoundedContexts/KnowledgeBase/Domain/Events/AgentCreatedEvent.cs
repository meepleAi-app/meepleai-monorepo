using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

public sealed class AgentCreatedEvent : DomainEventBase
{
    public Guid AgentId { get; }
    public string Type { get; }
    public string Name { get; }

    public AgentCreatedEvent(Guid agentId, string type, string name)
    {
        AgentId = agentId;
        Type = type;
        Name = name;
    }
}
