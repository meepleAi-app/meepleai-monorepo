using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Event raised when a session snapshot is created.
/// Issue #4755: SessionSnapshot - Delta-based History + State Reconstruction.
/// </summary>
internal sealed class SessionSnapshotCreatedEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public Guid SnapshotId { get; }
    public int SnapshotIndex { get; }
    public SnapshotTrigger TriggerType { get; }

    public SessionSnapshotCreatedEvent(
        Guid sessionId, Guid snapshotId, int snapshotIndex, SnapshotTrigger triggerType)
    {
        SessionId = sessionId;
        SnapshotId = snapshotId;
        SnapshotIndex = snapshotIndex;
        TriggerType = triggerType;
    }
}
