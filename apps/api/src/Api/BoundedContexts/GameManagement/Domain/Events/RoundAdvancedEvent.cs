using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when round number advances.
/// Issue #2405: Ledger Mode - Full State Tracking.
/// </summary>
internal sealed class RoundAdvancedEvent : DomainEventBase
{
    public Guid StateId { get; }
    public Guid SessionId { get; }
    public int OldRound { get; }
    public int NewRound { get; }

    public RoundAdvancedEvent(
        Guid stateId,
        Guid sessionId,
        int oldRound,
        int newRound)
    {
        StateId = stateId;
        SessionId = sessionId;
        OldRound = oldRound;
        NewRound = newRound;
    }
}
