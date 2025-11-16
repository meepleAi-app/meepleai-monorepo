using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

public sealed class AgentInvokedEvent : DomainEventBase
{
    public Guid AgentId { get; }
    public string Input { get; }
    public int TokensUsed { get; }

    public AgentInvokedEvent(Guid agentId, string input, int tokensUsed)
    {
        AgentId = agentId;
        Input = input;
        TokensUsed = tokensUsed;
    }
}
