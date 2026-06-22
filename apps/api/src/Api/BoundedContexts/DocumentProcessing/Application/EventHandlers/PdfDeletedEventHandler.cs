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

        var resourceKey = notification.CoverR2Key;

        // Mirror of PdfProcessingPipelineService upload pattern (linee 539-549):
        // resourceKey = "pdf-cover-{id}", fileId = "thumb.webp" | "preview.webp",
        // category = GameImage → S3 prefix "game-images/{resourceKey}/{fileId}_".
        await TryDeleteAsync("thumb.webp", resourceKey, notification.PdfDocumentId, cancellationToken).ConfigureAwait(false);
        await TryDeleteAsync("preview.webp", resourceKey, notification.PdfDocumentId, cancellationToken).ConfigureAwait(false);
    }

    private async Task TryDeleteAsync(string fileId, string resourceKey, Guid pdfDocumentId, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _blobStorage
                .DeleteAsync(fileId, BlobCategory.GameImage, resourceKey, cancellationToken)
                .ConfigureAwait(false);

            if (deleted)
            {
                _logger.LogInformation(
                    "Evicted cover blob {FileId} for PDF {PdfDocumentId} (resourceKey={ResourceKey}).",
                    fileId, pdfDocumentId, resourceKey);
            }
            else
            {
                _logger.LogDebug(
                    "Cover blob {FileId} for PDF {PdfDocumentId} was not present (resourceKey={ResourceKey}).",
                    fileId, pdfDocumentId, resourceKey);
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
                "Failed to delete cover blob {FileId} for PDF {PdfDocumentId} (resourceKey={ResourceKey}); leaving orphan.",
                fileId, pdfDocumentId, resourceKey);
        }
    }
}
