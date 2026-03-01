using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a paused live game session is resumed.
/// </summary>
internal sealed class LiveSessionResumedEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public DateTime ResumedAt { get; }

    public LiveSessionResumedEvent(Guid sessionId, DateTime resumedAt)
    {
        SessionId = sessionId;
        ResumedAt = resumedAt;
    }
}
