using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when the client app is backgrounded (app loses focus or closed).
/// </summary>
internal sealed class AppBackgroundedEvent : DomainEventBase
{
    public Guid SessionId { get; }

    public AppBackgroundedEvent(Guid sessionId)
    {
        SessionId = sessionId;
    }
}
