using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a paused session is resumed.
/// </summary>
internal sealed class SessionResumedEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public string? AgentRecap { get; }

    public SessionResumedEvent(Guid sessionId, string? agentRecap)
    {
        SessionId = sessionId;
        AgentRecap = agentRecap;
    }
}
