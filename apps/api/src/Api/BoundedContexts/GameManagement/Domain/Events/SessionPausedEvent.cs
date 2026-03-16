using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a session is paused (player initiated or timeout).
/// </summary>
internal sealed class SessionPausedEvent : DomainEventBase
{
    public Guid SessionId { get; }

    public SessionPausedEvent(Guid sessionId)
    {
        SessionId = sessionId;
    }
}
