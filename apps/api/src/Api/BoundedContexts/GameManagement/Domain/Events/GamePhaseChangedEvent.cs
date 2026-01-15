using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when game phase changes.
/// Issue #2405: Ledger Mode - Full State Tracking.
/// </summary>
internal sealed class GamePhaseChangedEvent : DomainEventBase
{
    public Guid StateId { get; }
    public Guid SessionId { get; }
    public GamePhase OldPhase { get; }
    public GamePhase NewPhase { get; }

    public GamePhaseChangedEvent(
        Guid stateId,
        Guid sessionId,
        GamePhase oldPhase,
        GamePhase newPhase)
    {
        StateId = stateId;
        SessionId = sessionId;
        OldPhase = oldPhase;
        NewPhase = newPhase;
    }
}
