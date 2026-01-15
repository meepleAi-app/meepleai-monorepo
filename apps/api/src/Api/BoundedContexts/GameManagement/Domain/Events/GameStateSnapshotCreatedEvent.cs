using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Event raised when a game state snapshot is created.
/// </summary>
internal sealed class GameStateSnapshotCreatedEvent : DomainEventBase
{
    public Guid StateId { get; }
    public Guid SnapshotId { get; }
    public int TurnNumber { get; }

    public GameStateSnapshotCreatedEvent(Guid stateId, Guid snapshotId, int turnNumber)
    {
        StateId = stateId;
        SnapshotId = snapshotId;
        TurnNumber = turnNumber;
    }
}
