using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a photo is uploaded to a PlayRecord (scoreboard capture, party shot).
/// #2436 PR-B (ADR-067).
/// </summary>
internal sealed class PlayRecordPhotoUploadedEvent : DomainEventBase
{
    public Guid PlayRecordId { get; }
    public Guid PhotoId { get; }
    public Guid UploadedByUserId { get; }

    public PlayRecordPhotoUploadedEvent(Guid playRecordId, Guid photoId, Guid uploadedByUserId)
    {
        PlayRecordId = playRecordId;
        PhotoId = photoId;
        UploadedByUserId = uploadedByUserId;
    }
}
