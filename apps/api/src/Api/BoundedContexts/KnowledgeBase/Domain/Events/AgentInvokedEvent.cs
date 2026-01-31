using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Domain event raised when an agent is invoked with token usage tracking.
/// Issue #1694: Enhanced with LLM token usage and cost tracking.
/// </summary>
internal sealed class AgentInvokedEvent : DomainEventBase
{
    public Guid AgentId { get; }
    public string Input { get; }
    public int TokensUsed { get; }
    public decimal EstimatedCost { get; }
    public string ModelId { get; }
    public string Provider { get; }

    public AgentInvokedEvent(
        Guid agentId,
        string input,
        int tokensUsed,
        decimal estimatedCost,
        string modelId,
        string provider)
    {
        AgentId = agentId;
        Input = input;
        TokensUsed = tokensUsed;
        EstimatedCost = estimatedCost;
        ModelId = modelId;
        Provider = provider;
    }
}
