using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a play record is started.
/// </summary>
internal sealed class PlayRecordStartedEvent : DomainEventBase
{
    public Guid RecordId { get; }
    public DateTime StartTime { get; }

    public PlayRecordStartedEvent(Guid recordId, DateTime startTime)
    {
        RecordId = recordId;
        StartTime = startTime;
    }
}
