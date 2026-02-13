using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using MediatR;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

internal class PdfNotificationEventHandler :
    INotificationHandler<PdfStateChangedEvent>,
    INotificationHandler<PdfFailedEvent>,
    INotificationHandler<PdfRetryInitiatedEvent>
{
    private readonly INotificationPreferencesRepository _preferencesRepo;
    private readonly INotificationRepository _notificationRepo;
    private readonly ILogger<PdfNotificationEventHandler> _logger;

    public PdfNotificationEventHandler(
        INotificationPreferencesRepository preferencesRepo,
        INotificationRepository notificationRepo,
        ILogger<PdfNotificationEventHandler> logger)
    {
        _preferencesRepo = preferencesRepo;
        _notificationRepo = notificationRepo;
        _logger = logger;
    }

    public async Task Handle(PdfStateChangedEvent evt, CancellationToken cancellationToken)
    {
        if (evt.NewState != PdfProcessingState.Ready) return;

        var prefs = await _preferencesRepo.GetByUserIdAsync(evt.UploadedByUserId, cancellationToken).ConfigureAwait(false);
        if (prefs?.InAppOnDocumentReady == true)
        {
            var notification = new Notification(
                id: Guid.NewGuid(),
                userId: evt.UploadedByUserId,
                type: NotificationType.PdfUploadCompleted,
                severity: NotificationSeverity.Success,
                title: "PDF Ready",
                message: "Your PDF is ready for AI queries"
            );
            await _notificationRepo.AddAsync(notification, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("PDF ready notification created for user {UserId}", evt.UploadedByUserId);
        }
    }

    public async Task Handle(PdfFailedEvent evt, CancellationToken cancellationToken)
    {
        var prefs = await _preferencesRepo.GetByUserIdAsync(evt.UploadedByUserId, cancellationToken).ConfigureAwait(false);
        if (prefs?.InAppOnDocumentFailed == true)
        {
            var notification = new Notification(
                id: Guid.NewGuid(),
                userId: evt.UploadedByUserId,
                type: NotificationType.ProcessingFailed,
                severity: NotificationSeverity.Error,
                title: "PDF Failed",
                message: $"Processing failed: {evt.ErrorMessage}"
            );
            await _notificationRepo.AddAsync(notification, cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("PDF failure notification created for user {UserId}", evt.UploadedByUserId);
        }
    }

    public async Task Handle(PdfRetryInitiatedEvent evt, CancellationToken cancellationToken)
    {
        var prefs = await _preferencesRepo.GetByUserIdAsync(evt.UploadedByUserId, cancellationToken).ConfigureAwait(false);
        if (prefs?.InAppOnRetryAvailable == true)
        {
            var notification = new Notification(
                id: Guid.NewGuid(),
                userId: evt.UploadedByUserId,
                type: NotificationType.PdfUploadCompleted,
                severity: NotificationSeverity.Info,
                title: "PDF Retry",
                message: $"Retry #{evt.RetryCount} started"
            );
            await _notificationRepo.AddAsync(notification, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("PDF retry notification created for user {UserId}", evt.UploadedByUserId);
        }
    }
}
