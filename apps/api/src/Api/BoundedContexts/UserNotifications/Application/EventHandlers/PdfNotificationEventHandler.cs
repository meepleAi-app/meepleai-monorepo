using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Handles PDF document events to send multi-channel notifications (in-app, email, push).
/// Issue #4220: Multi-channel notification system for PDF pipeline.
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
    private readonly IEmailService _emailService;
    private readonly ILogger<PdfNotificationEventHandler> _logger;

    public PdfNotificationEventHandler(
        INotificationPreferencesRepository preferencesRepo,
        INotificationRepository notificationRepo,
        IPdfDocumentRepository pdfRepo,
        IUserRepository userRepo,
        IEmailService emailService,
        ILogger<PdfNotificationEventHandler> logger)
    {
        _preferencesRepo = preferencesRepo;
        _notificationRepo = notificationRepo;
        _pdfRepo = pdfRepo;
        _userRepo = userRepo;
        _emailService = emailService;
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

        // Email Notification (best-effort)
        if (prefs.EmailOnDocumentReady)
        {
            try
            {
                await _emailService.SendPdfReadyEmailAsync(
                    user.Email,
                    user.DisplayName,
                    pdfDoc.FileName.Value,
                    evt.PdfDocumentId,
                    cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("PDF ready email sent to user {UserId}", evt.UploadedByUserId);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send PDF ready email to user {UserId}", evt.UploadedByUserId);
            }
#pragma warning restore CA1031
        }

        // Push Notification (future implementation - placeholder)
        if (prefs.PushOnDocumentReady)
        {
            _logger.LogInformation("Push notification would be sent (not yet implemented) for user {UserId}", evt.UploadedByUserId);
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

        // Email Notification (best-effort)
        if (prefs.EmailOnDocumentFailed)
        {
            try
            {
                await _emailService.SendPdfFailedEmailAsync(
                    user.Email,
                    user.DisplayName,
                    pdfDoc.FileName.Value,
                    evt.ErrorMessage,
                    cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("PDF failed email sent to user {UserId}", evt.UploadedByUserId);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send PDF failed email to user {UserId}", evt.UploadedByUserId);
            }
#pragma warning restore CA1031
        }

        // Push Notification (future implementation - placeholder)
        if (prefs.PushOnDocumentFailed)
        {
            _logger.LogInformation("Push notification would be sent (not yet implemented) for user {UserId}", evt.UploadedByUserId);
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

        // Email Notification (best-effort)
        if (prefs.EmailOnRetryAvailable)
        {
            try
            {
                await _emailService.SendPdfRetryEmailAsync(
                    user.Email,
                    user.DisplayName,
                    pdfDoc.FileName.Value,
                    evt.RetryCount,
                    cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("PDF retry email sent to user {UserId}", evt.UploadedByUserId);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send PDF retry email to user {UserId}", evt.UploadedByUserId);
            }
#pragma warning restore CA1031
        }

        // Push Notification (future implementation - placeholder)
        if (prefs.PushOnRetryAvailable)
        {
            _logger.LogInformation("Push notification would be sent (not yet implemented) for user {UserId}", evt.UploadedByUserId);
        }
    }
}
