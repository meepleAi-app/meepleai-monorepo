using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

internal sealed class AgentConfiguredEvent : DomainEventBase
{
    public Guid AgentId { get; }
    public string ConfigurationJson { get; }

    public AgentConfiguredEvent(Guid agentId, string configurationJson)
    {
        AgentId = agentId;
        ConfigurationJson = configurationJson;
    }
}
