using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Handles PDF document events to send multi-channel notifications (in-app, email via queue, push).
/// Issue #4220: Multi-channel notification system for PDF pipeline.
/// Issue #4417: Refactored to use email queue for async delivery.
/// </summary>
internal class PdfNotificationEventHandler :
    INotificationHandler<PdfStateChangedEvent>,
    INotificationHandler<PdfFailedEvent>,
    INotificationHandler<PdfRetryInitiatedEvent>
{
    private readonly INotificationPreferencesRepository _preferencesRepo;
    private readonly INotificationRepository _notificationRepo;
    private readonly IPdfDocumentRepository _pdfRepo;
    private readonly IUserRepository _userRepo;
    private readonly IMediator _mediator;
    private readonly IPushNotificationService _pushService;
    private readonly ILogger<PdfNotificationEventHandler> _logger;

    public PdfNotificationEventHandler(
        INotificationPreferencesRepository preferencesRepo,
        INotificationRepository notificationRepo,
        IPdfDocumentRepository pdfRepo,
        IUserRepository userRepo,
        IMediator mediator,
        IPushNotificationService pushService,
        ILogger<PdfNotificationEventHandler> logger)
    {
        _preferencesRepo = preferencesRepo;
        _notificationRepo = notificationRepo;
        _pdfRepo = pdfRepo;
        _userRepo = userRepo;
        _mediator = mediator;
        _pushService = pushService;
        _logger = logger;
    }

    public async Task Handle(PdfStateChangedEvent evt, CancellationToken cancellationToken)
    {
        if (evt.NewState != PdfProcessingState.Ready) return;

        var prefs = await _preferencesRepo.GetByUserIdAsync(evt.UploadedByUserId, cancellationToken).ConfigureAwait(false);
        if (prefs == null) return;

        // Get PDF and user details for notifications
        var pdfDoc = await _pdfRepo.GetByIdAsync(evt.PdfDocumentId, cancellationToken).ConfigureAwait(false);
        var user = await _userRepo.GetByIdAsync(evt.UploadedByUserId, cancellationToken).ConfigureAwait(false);

        if (pdfDoc == null || user == null)
        {
            _logger.LogWarning(
                "Cannot send PDF ready notifications: PDF {PdfId} or User {UserId} not found",
                evt.PdfDocumentId,
                evt.UploadedByUserId);
            return;
        }

        // In-App Notification
        if (prefs.InAppOnDocumentReady)
        {
            var notification = new Notification(
                id: Guid.NewGuid(),
                userId: evt.UploadedByUserId,
                type: NotificationType.PdfUploadCompleted,
                severity: NotificationSeverity.Success,
                title: "PDF Ready",
                message: $"Your PDF '{pdfDoc.FileName.Value}' is ready for AI queries",
                link: $"/documents/{evt.PdfDocumentId}"
            );
            await _notificationRepo.AddAsync(notification, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("PDF ready in-app notification created for user {UserId}", evt.UploadedByUserId);
        }

        // Email Notification via Queue (Issue #4417)
        if (prefs.EmailOnDocumentReady)
        {
            try
            {
                await _mediator.Send(new EnqueueEmailCommand(
                    UserId: evt.UploadedByUserId,
                    To: user.Email,
                    Subject: "Your PDF is Ready - MeepleAI",
                    TemplateName: "document_ready",
                    UserName: user.DisplayName,
                    FileName: pdfDoc.FileName.Value,
                    DocumentUrl: $"/documents/{evt.PdfDocumentId}"
                ), cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("PDF ready email enqueued for user {UserId}", evt.UploadedByUserId);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue PDF ready email for user {UserId}", evt.UploadedByUserId);
            }
#pragma warning restore CA1031
        }

        // Push Notification (Issue #4416)
        if (prefs.PushOnDocumentReady && prefs.HasPushSubscription)
        {
            try
            {
                await _pushService.SendPushNotificationAsync(
                    prefs.PushEndpoint!,
                    prefs.PushP256dhKey!,
                    prefs.PushAuthKey!,
                    "PDF Ready",
                    $"Your PDF '{pdfDoc.FileName.Value}' is ready for AI queries",
                    $"/documents/{evt.PdfDocumentId}",
                    cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send push notification for PDF ready to user {UserId}", evt.UploadedByUserId);
            }
#pragma warning restore CA1031
        }
    }

    public async Task Handle(PdfFailedEvent evt, CancellationToken cancellationToken)
    {
        var prefs = await _preferencesRepo.GetByUserIdAsync(evt.UploadedByUserId, cancellationToken).ConfigureAwait(false);
        if (prefs == null) return;

        // Get PDF and user details for notifications
        var pdfDoc = await _pdfRepo.GetByIdAsync(evt.PdfDocumentId, cancellationToken).ConfigureAwait(false);
        var user = await _userRepo.GetByIdAsync(evt.UploadedByUserId, cancellationToken).ConfigureAwait(false);

        if (pdfDoc == null || user == null)
        {
            _logger.LogWarning(
                "Cannot send PDF failed notifications: PDF {PdfId} or User {UserId} not found",
                evt.PdfDocumentId,
                evt.UploadedByUserId);
            return;
        }

        // In-App Notification
        if (prefs.InAppOnDocumentFailed)
        {
            var notification = new Notification(
                id: Guid.NewGuid(),
                userId: evt.UploadedByUserId,
                type: NotificationType.ProcessingFailed,
                severity: NotificationSeverity.Error,
                title: "PDF Processing Failed",
                message: $"Failed to process '{pdfDoc.FileName.Value}': {evt.ErrorMessage}",
                link: $"/documents/{evt.PdfDocumentId}"
            );
            await _notificationRepo.AddAsync(notification, cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("PDF failure in-app notification created for user {UserId}", evt.UploadedByUserId);
        }

        // Email Notification via Queue (Issue #4417)
        if (prefs.EmailOnDocumentFailed)
        {
            try
            {
                await _mediator.Send(new EnqueueEmailCommand(
                    UserId: evt.UploadedByUserId,
                    To: user.Email,
                    Subject: "PDF Processing Failed - MeepleAI",
                    TemplateName: "document_failed",
                    UserName: user.DisplayName,
                    FileName: pdfDoc.FileName.Value,
                    ErrorMessage: evt.ErrorMessage
                ), cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("PDF failed email enqueued for user {UserId}", evt.UploadedByUserId);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue PDF failed email for user {UserId}", evt.UploadedByUserId);
            }
#pragma warning restore CA1031
        }

        // Push Notification (Issue #4416)
        if (prefs.PushOnDocumentFailed && prefs.HasPushSubscription)
        {
            try
            {
                await _pushService.SendPushNotificationAsync(
                    prefs.PushEndpoint!,
                    prefs.PushP256dhKey!,
                    prefs.PushAuthKey!,
                    "PDF Processing Failed",
                    $"Failed to process '{pdfDoc.FileName.Value}': {evt.ErrorMessage}",
                    $"/documents/{evt.PdfDocumentId}",
                    cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send push notification for PDF failure to user {UserId}", evt.UploadedByUserId);
            }
#pragma warning restore CA1031
        }
    }

    public async Task Handle(PdfRetryInitiatedEvent evt, CancellationToken cancellationToken)
    {
        var prefs = await _preferencesRepo.GetByUserIdAsync(evt.UploadedByUserId, cancellationToken).ConfigureAwait(false);
        if (prefs == null) return;

        // Get PDF and user details for notifications
        var pdfDoc = await _pdfRepo.GetByIdAsync(evt.PdfDocumentId, cancellationToken).ConfigureAwait(false);
        var user = await _userRepo.GetByIdAsync(evt.UploadedByUserId, cancellationToken).ConfigureAwait(false);

        if (pdfDoc == null || user == null)
        {
            _logger.LogWarning(
                "Cannot send PDF retry notifications: PDF {PdfId} or User {UserId} not found",
                evt.PdfDocumentId,
                evt.UploadedByUserId);
            return;
        }

        // In-App Notification
        if (prefs.InAppOnRetryAvailable)
        {
            var notification = new Notification(
                id: Guid.NewGuid(),
                userId: evt.UploadedByUserId,
                type: NotificationType.PdfUploadCompleted,
                severity: NotificationSeverity.Info,
                title: "PDF Retry Started",
                message: $"Retrying '{pdfDoc.FileName.Value}' (Attempt #{evt.RetryCount})",
                link: $"/documents/{evt.PdfDocumentId}"
            );
            await _notificationRepo.AddAsync(notification, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("PDF retry in-app notification created for user {UserId}", evt.UploadedByUserId);
        }

        // Email Notification via Queue (Issue #4417)
        if (prefs.EmailOnRetryAvailable)
        {
            try
            {
                await _mediator.Send(new EnqueueEmailCommand(
                    UserId: evt.UploadedByUserId,
                    To: user.Email,
                    Subject: "PDF Retry - MeepleAI",
                    TemplateName: "retry_available",
                    UserName: user.DisplayName,
                    FileName: pdfDoc.FileName.Value,
                    RetryCount: evt.RetryCount
                ), cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("PDF retry email enqueued for user {UserId}", evt.UploadedByUserId);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue PDF retry email for user {UserId}", evt.UploadedByUserId);
            }
#pragma warning restore CA1031
        }

        // Push Notification (Issue #4416)
        if (prefs.PushOnRetryAvailable && prefs.HasPushSubscription)
        {
            try
            {
                await _pushService.SendPushNotificationAsync(
                    prefs.PushEndpoint!,
                    prefs.PushP256dhKey!,
                    prefs.PushAuthKey!,
                    "PDF Retry Started",
                    $"Retrying '{pdfDoc.FileName.Value}' (Attempt #{evt.RetryCount})",
                    $"/documents/{evt.PdfDocumentId}",
                    cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send push notification for PDF retry to user {UserId}", evt.UploadedByUserId);
            }
#pragma warning restore CA1031
        }
    }
}
