using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Event raised when game session state is restored from a snapshot.
/// </summary>
internal sealed class GameStateRestoredEvent : DomainEventBase
{
    public Guid StateId { get; }
    public Guid SnapshotId { get; }
    public int TurnNumber { get; }

    public GameStateRestoredEvent(Guid stateId, Guid snapshotId, int turnNumber)
    {
        StateId = stateId;
        SnapshotId = snapshotId;
        TurnNumber = turnNumber;
    }
}
