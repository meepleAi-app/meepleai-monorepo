using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Event raised when a session attachment (photo) is uploaded.
/// Issue #5359 - SessionAttachment domain entity.
/// </summary>
internal sealed class SessionAttachmentCreatedEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public Guid AttachmentId { get; }
    public Guid PlayerId { get; }
    public AttachmentType AttachmentType { get; }

    public SessionAttachmentCreatedEvent(
        Guid sessionId, Guid attachmentId, Guid playerId, AttachmentType attachmentType)
    {
        SessionId = sessionId;
        AttachmentId = attachmentId;
        PlayerId = playerId;
        AttachmentType = attachmentType;
    }
}
