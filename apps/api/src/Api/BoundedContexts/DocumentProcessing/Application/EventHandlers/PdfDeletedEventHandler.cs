using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.Services.Pdf;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;

/// <summary>
/// Issue #1831 AC — removes the L4 cover thumb/preview blobs from R2 once the
/// owning PDF document has been deleted. Best-effort: a deletion failure must
/// not propagate back into the originating <c>DeletePdfCommandHandler</c>
/// since the persistence side is already committed by the time the event
/// reaches this handler.
/// </summary>
internal sealed class PdfDeletedEventHandler : INotificationHandler<PdfDeletedDomainEvent>
{
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<PdfDeletedEventHandler> _logger;

    public PdfDeletedEventHandler(
        IBlobStorageService blobStorage,
        ILogger<PdfDeletedEventHandler> logger)
    {
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(PdfDeletedDomainEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (string.IsNullOrWhiteSpace(notification.CoverR2Key))
        {
            _logger.LogDebug(
                "PdfDeletedDomainEvent for PDF {PdfDocumentId} has no CoverR2Key; nothing to evict.",
                notification.PdfDocumentId);
            return;
        }

        // Issue #2947: CoverR2Key is now the deterministic, un-suffixed key
        // ("covers/pdf/{id:D}/cover") written by PdfProcessingPipelineService /
        // BackfillPdfCoversJob. The ONLY physical object is the preview variant
        // ("{key}-preview.webp") — there is no separate thumb object anymore.
        // The categorized DeleteAsync cannot be used because it runs
        // PathSecurity.ValidateIdentifier, which rejects '/' and '.'; delete via
        // the raw-key primitive instead (mirrors CoverUrlResolver's raw-key reads).
        var rawKey = $"{notification.CoverR2Key}-preview.webp";
        await TryDeleteRawKeyAsync(rawKey, notification.PdfDocumentId, cancellationToken).ConfigureAwait(false);
    }

    private async Task TryDeleteRawKeyAsync(string rawKey, Guid pdfDocumentId, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _blobStorage
                .DeleteRawKeyAsync(rawKey, cancellationToken)
                .ConfigureAwait(false);

            if (deleted)
            {
                _logger.LogInformation(
                    "Evicted cover blob for PDF {PdfDocumentId} (rawKey={RawKey}).",
                    pdfDocumentId, rawKey);
            }
            else
            {
                _logger.LogDebug(
                    "Cover blob for PDF {PdfDocumentId} was not present (rawKey={RawKey}).",
                    pdfDocumentId, rawKey);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // CLEANUP PATTERN: storage cleanup is best-effort. The PDF metadata is
        // already gone; a blob delete failure here would only leave an orphan
        // R2 object that the backfill job can re-evaluate later.
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex,
                "Failed to delete cover blob for PDF {PdfDocumentId} (rawKey={RawKey}); leaving orphan.",
                pdfDocumentId, rawKey);
        }
    }
}
