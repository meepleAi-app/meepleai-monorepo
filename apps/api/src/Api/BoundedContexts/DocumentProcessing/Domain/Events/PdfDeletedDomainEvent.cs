using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Events;

/// <summary>
/// Raised when a PDF document has been removed from the persistence store.
/// Consumed by <c>PdfDeletedEventHandler</c> to evict the associated L4 cover
/// artefacts from blob storage (thumb + preview), so the bucket does not
/// accumulate orphaned <c>game-images/pdf-cover-{id}/*</c> objects.
/// </summary>
/// <remarks>
/// Issue #1831 AC: "Storage cleanup on PDF delete (R2 object removal in event handler)".
/// Pattern coerente con <see cref="PdfCoverGeneratedEvent"/> introdotto in #1852.
/// </remarks>
internal sealed class PdfDeletedDomainEvent : DomainEventBase
{
    public Guid PdfDocumentId { get; }

    /// <summary>
    /// Resource-key prefix used when the cover was uploaded
    /// (<c>pdf-cover-{PdfDocumentId}</c>). Null when the PDF never produced a
    /// cover (e.g. Skipped or Pending status at delete time) — the handler
    /// then has no-op work to do.
    /// </summary>
    public string? CoverR2Key { get; }

    public PdfDeletedDomainEvent(Guid pdfDocumentId, string? coverR2Key)
    {
        PdfDocumentId = pdfDocumentId;
        CoverR2Key = coverR2Key;
    }
}
