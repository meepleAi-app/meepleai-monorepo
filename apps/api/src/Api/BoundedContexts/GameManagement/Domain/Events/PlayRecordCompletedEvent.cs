using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a play record is completed.
/// </summary>
internal sealed class PlayRecordCompletedEvent : DomainEventBase
{
    public Guid RecordId { get; }
    public TimeSpan Duration { get; }

    public PlayRecordCompletedEvent(Guid recordId, TimeSpan duration)
    {
        RecordId = recordId;
        Duration = duration;
    }
}
