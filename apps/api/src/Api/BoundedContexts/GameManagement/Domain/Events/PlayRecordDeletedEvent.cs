using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a play record is soft-deleted (issue #2439).
/// </summary>
internal sealed class PlayRecordDeletedEvent : DomainEventBase
{
    public Guid RecordId { get; }
    public Guid DeletedByUserId { get; }

    public PlayRecordDeletedEvent(Guid recordId, Guid deletedByUserId)
    {
        RecordId = recordId;
        DeletedByUserId = deletedByUserId;
    }
}
