using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a play record's details are updated.
/// </summary>
internal sealed class PlayRecordUpdatedEvent : DomainEventBase
{
    public Guid RecordId { get; }

    public PlayRecordUpdatedEvent(Guid recordId)
    {
        RecordId = recordId;
    }
}
