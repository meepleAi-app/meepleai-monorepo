using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a player is removed from a live game session.
/// </summary>
internal sealed class LiveSessionPlayerRemovedEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public Guid PlayerId { get; }

    public LiveSessionPlayerRemovedEvent(Guid sessionId, Guid playerId)
    {
        SessionId = sessionId;
        PlayerId = playerId;
    }
}
