using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a live game session is paused.
/// </summary>
internal sealed class LiveSessionPausedEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public DateTime PausedAt { get; }

    public LiveSessionPausedEvent(Guid sessionId, DateTime pausedAt)
    {
        SessionId = sessionId;
        PausedAt = pausedAt;
    }
}
