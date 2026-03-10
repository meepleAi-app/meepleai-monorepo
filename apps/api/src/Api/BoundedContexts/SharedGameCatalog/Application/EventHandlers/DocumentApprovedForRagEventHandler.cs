using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Event handler that sets IsActiveForRag=true on the associated PdfDocument
/// when a SharedGame document is approved for RAG processing.
/// Issue #98: DocumentApprovedForRagEvent was published but had no handler.
/// </summary>
internal sealed class DocumentApprovedForRagEventHandler : INotificationHandler<DocumentApprovedForRagEvent>
{
    private readonly IPdfDocumentRepository _pdfDocumentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DocumentApprovedForRagEventHandler> _logger;

    public DocumentApprovedForRagEventHandler(
        IPdfDocumentRepository pdfDocumentRepository,
        IUnitOfWork unitOfWork,
        ILogger<DocumentApprovedForRagEventHandler> logger)
    {
        _pdfDocumentRepository = pdfDocumentRepository ?? throw new ArgumentNullException(nameof(pdfDocumentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(DocumentApprovedForRagEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing DocumentApprovedForRagEvent: DocumentId={DocumentId}, PdfDocumentId={PdfDocumentId}, SharedGameId={SharedGameId}",
            notification.DocumentId, notification.PdfDocumentId, notification.SharedGameId);

        try
        {
            var pdfDocument = await _pdfDocumentRepository.GetByIdAsync(
                notification.PdfDocumentId, cancellationToken).ConfigureAwait(false);

            if (pdfDocument is null)
            {
                _logger.LogWarning(
                    "PdfDocument {PdfDocumentId} not found for DocumentApprovedForRagEvent. " +
                    "The PDF may have been deleted after approval was initiated.",
                    notification.PdfDocumentId);
                return;
            }

            if (pdfDocument.IsActiveForRag)
            {
                _logger.LogInformation(
                    "PdfDocument {PdfDocumentId} is already active for RAG, skipping update",
                    notification.PdfDocumentId);
                return;
            }

            pdfDocument.SetActiveForRag(true);
            await _pdfDocumentRepository.UpdateAsync(pdfDocument, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Successfully set IsActiveForRag=true on PdfDocument {PdfDocumentId} " +
                "for SharedGame {SharedGameId}, approved by {ApprovedBy}",
                notification.PdfDocumentId, notification.SharedGameId, notification.ApprovedBy);
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
