using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when the turn order is changed in a live game session.
/// </summary>
internal sealed class LiveSessionTurnOrderChangedEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public IReadOnlyList<Guid> NewTurnOrder { get; }

    public LiveSessionTurnOrderChangedEvent(Guid sessionId, IReadOnlyList<Guid> newTurnOrder)
    {
        SessionId = sessionId;
        NewTurnOrder = newTurnOrder;
    }
}
