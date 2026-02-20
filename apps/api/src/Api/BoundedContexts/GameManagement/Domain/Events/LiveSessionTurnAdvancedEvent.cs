using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when the turn advances in a live game session.
/// </summary>
internal sealed class LiveSessionTurnAdvancedEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public int NewTurnIndex { get; }
    public Guid CurrentPlayerId { get; }

    public LiveSessionTurnAdvancedEvent(Guid sessionId, int newTurnIndex, Guid currentPlayerId)
    {
        SessionId = sessionId;
        NewTurnIndex = newTurnIndex;
        CurrentPlayerId = currentPlayerId;
    }
}
