using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Raised when a PDF has been downloaded and stored, ready for DocumentProcessing pipeline.
/// DocumentProcessing subscribes to this event to trigger extraction, chunking, and indexing.
/// </summary>
internal sealed class PdfReadyForProcessingEvent : DomainEventBase
{
    public Guid PdfDocumentId { get; }
    public Guid SharedGameId { get; }
    public Guid UserId { get; }

    public PdfReadyForProcessingEvent(Guid pdfDocumentId, Guid sharedGameId, Guid userId)
    {
        PdfDocumentId = pdfDocumentId;
        SharedGameId = sharedGameId;
        UserId = userId;
    }
}
