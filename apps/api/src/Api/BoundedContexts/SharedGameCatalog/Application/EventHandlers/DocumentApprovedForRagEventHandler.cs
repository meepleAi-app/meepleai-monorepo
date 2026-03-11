using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Event handler that sets IsActiveForRag=true on the associated PdfDocument
/// when a SharedGame document is approved for RAG processing.
/// Issue #98: DocumentApprovedForRagEvent was published but had no handler.
/// Delegates to SetActiveForRagCommand in DocumentProcessing BC via MediatR,
/// avoiding direct cross-BC repository coupling.
/// </summary>
internal sealed class DocumentApprovedForRagEventHandler : INotificationHandler<DocumentApprovedForRagEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<DocumentApprovedForRagEventHandler> _logger;

    public DocumentApprovedForRagEventHandler(
        IMediator mediator,
        ILogger<DocumentApprovedForRagEventHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(DocumentApprovedForRagEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing DocumentApprovedForRagEvent: DocumentId={DocumentId}, PdfDocumentId={PdfDocumentId}, SharedGameId={SharedGameId}",
            notification.DocumentId, notification.PdfDocumentId, notification.SharedGameId);

        try
        {
            var result = await _mediator.Send(
                new SetActiveForRagCommand(notification.PdfDocumentId, true),
                cancellationToken).ConfigureAwait(false);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Successfully set IsActiveForRag=true on PdfDocument {PdfDocumentId} " +
                    "for SharedGame {SharedGameId}, approved by {ApprovedBy}",
                    notification.PdfDocumentId, notification.SharedGameId, notification.ApprovedBy);
            }
            else
            {
                _logger.LogWarning(
                    "SetActiveForRag returned failure for PdfDocument {PdfDocumentId}: {Message}",
                    notification.PdfDocumentId, result.Message);
            }
        }
        catch (Exception ex)
        {
            // RAG activation failure should NOT fail the approval flow
            _logger.LogError(
                ex,
                "Failed to set IsActiveForRag on PdfDocument {PdfDocumentId} for document approval {DocumentId}. " +
                "Manual activation may be required.",
                notification.PdfDocumentId, notification.DocumentId);
        }
    }
}
