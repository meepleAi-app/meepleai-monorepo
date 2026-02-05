using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.UserLibrary.Application.EventHandlers;

/// <summary>
/// Handles PrivatePdfRemovedEvent to cleanup vectors from private_rules collection.
/// Issue #3651: Ensures vector data isolation is maintained when private PDFs are removed.
/// </summary>
internal sealed class PrivatePdfRemovedEventHandler : INotificationHandler<PrivatePdfRemovedEvent>
{
    private readonly IPrivateQdrantService _privateQdrantService;
    private readonly ILogger<PrivatePdfRemovedEventHandler> _logger;

    public PrivatePdfRemovedEventHandler(
        IPrivateQdrantService privateQdrantService,
        ILogger<PrivatePdfRemovedEventHandler> logger)
    {
        _privateQdrantService = privateQdrantService ?? throw new ArgumentNullException(nameof(privateQdrantService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(PrivatePdfRemovedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing PrivatePdfRemovedEvent: User={UserId}, PDF={PdfId}, LibraryEntry={EntryId}",
            notification.UserId, notification.PdfDocumentId, notification.LibraryEntryId);

        try
        {
            // Delete all vectors for this PDF from the private_rules collection
            var deleted = await _privateQdrantService.DeletePrivateByPdfIdAsync(
                notification.UserId,
                notification.PdfDocumentId,
                cancellationToken).ConfigureAwait(false);

            if (deleted)
            {
                _logger.LogInformation(
                    "Successfully deleted private vectors for PDF {PdfId} from private_rules collection",
                    notification.PdfDocumentId);
            }
            else
            {
                _logger.LogWarning(
                    "No vectors found or failed to delete private vectors for PDF {PdfId}. " +
                    "This may indicate the PDF was never indexed or already deleted.",
                    notification.PdfDocumentId);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail the transaction
            // The PDF association is already removed; vector cleanup failure is non-critical
            _logger.LogError(
                ex,
                "Failed to delete private vectors for PDF {PdfId}. Manual cleanup may be required.",
                notification.PdfDocumentId);
        }
    }
}
