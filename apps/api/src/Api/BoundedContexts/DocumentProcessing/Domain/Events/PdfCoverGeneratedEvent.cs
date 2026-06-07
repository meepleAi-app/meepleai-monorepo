using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Events;

/// <summary>
/// Raised when the L4 PDF cover has been rendered and uploaded to R2.
/// Consumed by <c>PdfCoverGeneratedEventHandler</c> in the SharedGameCatalog BC
/// to propagate the cover key to <c>SharedGame.PdfCoverR2Key</c>.
/// </summary>
internal sealed class PdfCoverGeneratedEvent : DomainEventBase
{
    public Guid PdfDocumentId { get; }
    public Guid? SharedGameId { get; }
    public string CoverR2Key { get; }
    public int CoverPageIndex { get; }

    public PdfCoverGeneratedEvent(Guid pdfDocumentId, Guid? sharedGameId, string coverR2Key, int coverPageIndex)
    {
        PdfDocumentId = pdfDocumentId;
        SharedGameId = sharedGameId;
        CoverR2Key = coverR2Key;
        CoverPageIndex = coverPageIndex;
    }
}
