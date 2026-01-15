using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a player's score is updated.
/// Issue #2405: Ledger Mode - Full State Tracking.
/// </summary>
internal sealed class PlayerScoreUpdatedEvent : DomainEventBase
{
    public Guid StateId { get; }
    public Guid SessionId { get; }
    public string PlayerName { get; }
    public int OldScore { get; }
    public int NewScore { get; }

    public PlayerScoreUpdatedEvent(
        Guid stateId,
        Guid sessionId,
        string playerName,
        int oldScore,
        int newScore)
    {
        StateId = stateId;
        SessionId = sessionId;
        PlayerName = playerName;
        OldScore = oldScore;
        NewScore = newScore;
    }
}
