using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Raised when the agent type is switched mid-conversation in a chat thread (Issue #4465).
/// </summary>
internal sealed class AgentSwitchedInThreadEvent : DomainEventBase
{
    public Guid ThreadId { get; }
    public string? PreviousAgentType { get; }
    public string NewAgentType { get; }

    public AgentSwitchedInThreadEvent(Guid threadId, string? previousAgentType, string newAgentType)
    {
        ThreadId = threadId;
        PreviousAgentType = previousAgentType;
        NewAgentType = newAgentType;
    }
}
