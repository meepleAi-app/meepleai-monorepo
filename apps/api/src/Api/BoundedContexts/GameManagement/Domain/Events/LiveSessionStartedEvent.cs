using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a live game session transitions to InProgress.
/// </summary>
internal sealed class LiveSessionStartedEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public DateTime StartedAt { get; }

    public LiveSessionStartedEvent(Guid sessionId, DateTime startedAt)
    {
        SessionId = sessionId;
        StartedAt = startedAt;
    }
}
