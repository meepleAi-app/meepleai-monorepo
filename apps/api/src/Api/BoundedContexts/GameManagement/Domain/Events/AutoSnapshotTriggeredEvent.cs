using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when an automatic snapshot is triggered.
/// Issue #4761: Turn Phases from TurnTemplate + Event-Triggered Snapshots.
/// </summary>
internal sealed class AutoSnapshotTriggeredEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public SnapshotTrigger TriggerType { get; }
    public string? Description { get; }

    public AutoSnapshotTriggeredEvent(Guid sessionId, SnapshotTrigger triggerType, string? description)
    {
        SessionId = sessionId;
        TriggerType = triggerType;
        Description = description;
    }
}
