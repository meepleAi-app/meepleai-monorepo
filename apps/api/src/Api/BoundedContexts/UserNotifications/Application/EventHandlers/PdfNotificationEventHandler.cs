using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using MediatR;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Handles PDF document events to send multi-channel notifications via NotificationDispatcher.
/// Issue #4220: Multi-channel notification system for PDF pipeline.
/// </summary>
internal class PdfNotificationEventHandler :
    INotificationHandler<PdfStateChangedEvent>,
    INotificationHandler<PdfFailedEvent>,
    INotificationHandler<PdfRetryInitiatedEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly IPdfDocumentRepository _pdfRepo;
    private readonly ILogger<PdfNotificationEventHandler> _logger;

    public PdfNotificationEventHandler(
        INotificationDispatcher dispatcher,
        IPdfDocumentRepository pdfRepo,
        ILogger<PdfNotificationEventHandler> logger)
    {
        _dispatcher = dispatcher;
        _pdfRepo = pdfRepo;
        _logger = logger;
    }

    public async Task Handle(PdfStateChangedEvent evt, CancellationToken cancellationToken)
    {
        if (evt.NewState != PdfProcessingState.Ready) return;

        var pdfDoc = await _pdfRepo.GetByIdAsync(evt.PdfDocumentId, cancellationToken).ConfigureAwait(false);
        if (pdfDoc == null)
        {
            _logger.LogWarning("PDF {PdfId} not found for ready notification", evt.PdfDocumentId);
            return;
        }

        await _dispatcher.DispatchAsync(new NotificationMessage
        {
            Type = NotificationType.PdfUploadCompleted,
            RecipientUserId = evt.UploadedByUserId,
            Payload = new PdfProcessingPayload(
                evt.PdfDocumentId,
                pdfDoc.FileName.Value,
                "Ready"),
            DeepLinkPath = $"/documents/{evt.PdfDocumentId}"
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Dispatched PDF ready notification for user {UserId}", evt.UploadedByUserId);
    }

    public async Task Handle(PdfFailedEvent evt, CancellationToken cancellationToken)
    {
        var pdfDoc = await _pdfRepo.GetByIdAsync(evt.PdfDocumentId, cancellationToken).ConfigureAwait(false);
        if (pdfDoc == null)
        {
            _logger.LogWarning("PDF {PdfId} not found for failure notification", evt.PdfDocumentId);
            return;
        }

        await _dispatcher.DispatchAsync(new NotificationMessage
        {
            Type = NotificationType.ProcessingFailed,
            RecipientUserId = evt.UploadedByUserId,
            Payload = new PdfProcessingPayload(
                evt.PdfDocumentId,
                pdfDoc.FileName.Value,
                $"Failed: {evt.ErrorMessage}"),
            DeepLinkPath = $"/documents/{evt.PdfDocumentId}"
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogWarning("Dispatched PDF failure notification for user {UserId}", evt.UploadedByUserId);
    }

    public async Task Handle(PdfRetryInitiatedEvent evt, CancellationToken cancellationToken)
    {
        var pdfDoc = await _pdfRepo.GetByIdAsync(evt.PdfDocumentId, cancellationToken).ConfigureAwait(false);
        if (pdfDoc == null)
        {
            _logger.LogWarning("PDF {PdfId} not found for retry notification", evt.PdfDocumentId);
            return;
        }

        await _dispatcher.DispatchAsync(new NotificationMessage
        {
            Type = NotificationType.PdfUploadCompleted,
            RecipientUserId = evt.UploadedByUserId,
            Payload = new PdfProcessingPayload(
                evt.PdfDocumentId,
                pdfDoc.FileName.Value,
                $"Retry #{evt.RetryCount}"),
            DeepLinkPath = $"/documents/{evt.PdfDocumentId}"
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Dispatched PDF retry notification for user {UserId}", evt.UploadedByUserId);
    }
}
