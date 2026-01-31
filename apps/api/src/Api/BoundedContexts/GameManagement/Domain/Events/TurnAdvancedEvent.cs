using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when turn advances to next player.
/// Issue #2405: Ledger Mode - Full State Tracking.
/// </summary>
internal sealed class TurnAdvancedEvent : DomainEventBase
{
    public Guid StateId { get; }
    public Guid SessionId { get; }
    public string? PreviousPlayer { get; }
    public string CurrentPlayer { get; }

    public TurnAdvancedEvent(
        Guid stateId,
        Guid sessionId,
        string? previousPlayer,
        string currentPlayer)
    {
        StateId = stateId;
        SessionId = sessionId;
        PreviousPlayer = previousPlayer;
        CurrentPlayer = currentPlayer;
    }
}
