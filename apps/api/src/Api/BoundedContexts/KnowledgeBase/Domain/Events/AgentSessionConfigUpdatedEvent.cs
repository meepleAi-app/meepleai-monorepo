using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

internal sealed class AgentSessionConfigUpdatedEvent : DomainEventBase
{
    public Guid AgentSessionId { get; }
    public string ConfigJson { get; }

    public AgentSessionConfigUpdatedEvent(Guid agentSessionId, string configJson)
    {
        AgentSessionId = agentSessionId;
        ConfigJson = configJson ?? throw new ArgumentNullException(nameof(configJson));
    }
}
