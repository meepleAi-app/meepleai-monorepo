using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Raised when a PDF has been successfully associated with a SharedGame.
/// Used to update the HasUploadedPdf cached flag on SharedGame.
/// </summary>
internal sealed class SharedGamePdfUploadedEvent : DomainEventBase
{
    public Guid SharedGameId { get; }

    public SharedGamePdfUploadedEvent(Guid sharedGameId)
    {
        SharedGameId = sharedGameId;
    }
}
