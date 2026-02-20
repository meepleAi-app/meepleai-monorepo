using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Handles ProcessingJob domain events to send multi-channel notifications (in-app, email via queue, push).
/// Notifies the uploader when their PDF processing job completes or fails.
/// Issue #4736: Processing notifications - in-app, email, push.
/// </summary>
internal class ProcessingJobNotificationEventHandler :
    INotificationHandler<JobCompletedEvent>,
    INotificationHandler<JobFailedEvent>
{
    private readonly INotificationPreferencesRepository _preferencesRepo;
    private readonly INotificationRepository _notificationRepo;
    private readonly IPdfDocumentRepository _pdfRepo;
    private readonly IUserRepository _userRepo;
    private readonly IMediator _mediator;
    private readonly IPushNotificationService _pushService;
    private readonly ILogger<ProcessingJobNotificationEventHandler> _logger;

    public ProcessingJobNotificationEventHandler(
        INotificationPreferencesRepository preferencesRepo,
        INotificationRepository notificationRepo,
        IPdfDocumentRepository pdfRepo,
        IUserRepository userRepo,
        IMediator mediator,
        IPushNotificationService pushService,
        ILogger<ProcessingJobNotificationEventHandler> logger)
    {
        _preferencesRepo = preferencesRepo;
        _notificationRepo = notificationRepo;
        _pdfRepo = pdfRepo;
        _userRepo = userRepo;
        _mediator = mediator;
        _pushService = pushService;
        _logger = logger;
    }

    public async Task Handle(JobCompletedEvent evt, CancellationToken cancellationToken)
    {
        var prefs = await _preferencesRepo.GetByUserIdAsync(evt.UserId, cancellationToken).ConfigureAwait(false);
        if (prefs == null) return;

        var pdfDoc = await _pdfRepo.GetByIdAsync(evt.PdfDocumentId, cancellationToken).ConfigureAwait(false);
        var user = await _userRepo.GetByIdAsync(evt.UserId, cancellationToken).ConfigureAwait(false);

        if (pdfDoc == null || user == null)
        {
            _logger.LogWarning(
                "Cannot send job completed notifications: PDF {PdfId} or User {UserId} not found",
                evt.PdfDocumentId, evt.UserId);
            return;
        }

        var fileName = pdfDoc.FileName.Value;
        var durationText = FormatDuration(evt.TotalDuration);

        // In-App Notification
        if (prefs.InAppOnDocumentReady)
        {
            try
            {
                var notification = new Notification(
                    id: Guid.NewGuid(),
                    userId: evt.UserId,
                    type: NotificationType.ProcessingJobCompleted,
                    severity: NotificationSeverity.Success,
                    title: "Processing Complete",
                    message: $"'{fileName}' processed successfully in {durationText}. Ready for AI queries.",
                    link: $"/admin/knowledge-base/queue"
                );
                await _notificationRepo.AddAsync(notification, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Job completed in-app notification created for user {UserId}, job {JobId}", evt.UserId, evt.JobId);
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create in-app notification for job completed, user {UserId}, job {JobId}", evt.UserId, evt.JobId);
            }
#pragma warning restore CA1031
        }

        // Email Notification via Queue
        if (prefs.EmailOnDocumentReady)
        {
            try
            {
                await _mediator.Send(new EnqueueEmailCommand(
                    UserId: evt.UserId,
                    To: user.Email,
                    Subject: $"PDF Processing Complete: {fileName} - MeepleAI",
                    TemplateName: "processing_job_completed",
                    UserName: user.DisplayName,
                    FileName: fileName,
                    DocumentUrl: $"/admin/knowledge-base/queue"
                ), cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Job completed email enqueued for user {UserId}, job {JobId}", evt.UserId, evt.JobId);
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue job completed email for user {UserId}, job {JobId}", evt.UserId, evt.JobId);
            }
#pragma warning restore CA1031
        }

        // Push Notification
        if (prefs.PushOnDocumentReady && prefs.HasPushSubscription)
        {
            try
            {
                await _pushService.SendPushNotificationAsync(
                    prefs.PushEndpoint!,
                    prefs.PushP256dhKey!,
                    prefs.PushAuthKey!,
                    "MeepleAI - PDF Ready",
                    $"'{fileName}' processed in {durationText}. Ready for AI queries.",
                    "/admin/knowledge-base/queue",
                    cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send push notification for job completed to user {UserId}", evt.UserId);
            }
#pragma warning restore CA1031
        }
    }

    public async Task Handle(JobFailedEvent evt, CancellationToken cancellationToken)
    {
        var prefs = await _preferencesRepo.GetByUserIdAsync(evt.UserId, cancellationToken).ConfigureAwait(false);
        if (prefs == null) return;

        var pdfDoc = await _pdfRepo.GetByIdAsync(evt.PdfDocumentId, cancellationToken).ConfigureAwait(false);
        var user = await _userRepo.GetByIdAsync(evt.UserId, cancellationToken).ConfigureAwait(false);

        if (pdfDoc == null || user == null)
        {
            _logger.LogWarning(
                "Cannot send job failed notifications: PDF {PdfId} or User {UserId} not found",
                evt.PdfDocumentId, evt.UserId);
            return;
        }

        var fileName = pdfDoc.FileName.Value;
        var stepText = evt.FailedAtStep != null ? $" at {evt.FailedAtStep}" : "";

        // In-App Notification
        if (prefs.InAppOnDocumentFailed)
        {
            try
            {
                var notification = new Notification(
                    id: Guid.NewGuid(),
                    userId: evt.UserId,
                    type: NotificationType.ProcessingJobFailed,
                    severity: NotificationSeverity.Error,
                    title: "Processing Failed",
                    message: $"'{fileName}' failed{stepText}: {evt.ErrorMessage}",
                    link: $"/admin/knowledge-base/queue"
                );
                await _notificationRepo.AddAsync(notification, cancellationToken).ConfigureAwait(false);
                _logger.LogWarning("Job failed in-app notification created for user {UserId}, job {JobId}", evt.UserId, evt.JobId);
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create in-app notification for job failed, user {UserId}, job {JobId}", evt.UserId, evt.JobId);
            }
#pragma warning restore CA1031
        }

        // Email Notification via Queue
        if (prefs.EmailOnDocumentFailed)
        {
            try
            {
                await _mediator.Send(new EnqueueEmailCommand(
                    UserId: evt.UserId,
                    To: user.Email,
                    Subject: $"PDF Processing Failed: {fileName} - MeepleAI",
                    TemplateName: "processing_job_failed",
                    UserName: user.DisplayName,
                    FileName: fileName,
                    ErrorMessage: $"Failed{stepText}: {evt.ErrorMessage}"
                ), cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Job failed email enqueued for user {UserId}, job {JobId}", evt.UserId, evt.JobId);
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue job failed email for user {UserId}, job {JobId}", evt.UserId, evt.JobId);
            }
#pragma warning restore CA1031
        }

        // Push Notification
        if (prefs.PushOnDocumentFailed && prefs.HasPushSubscription)
        {
            try
            {
                var truncatedError = TruncateForPush(evt.ErrorMessage);
                await _pushService.SendPushNotificationAsync(
                    prefs.PushEndpoint!,
                    prefs.PushP256dhKey!,
                    prefs.PushAuthKey!,
                    "MeepleAI - Processing Failed",
                    $"'{fileName}' failed{stepText}: {truncatedError}",
                    "/admin/knowledge-base/queue",
                    cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send push notification for job failed to user {UserId}", evt.UserId);
            }
#pragma warning restore CA1031
        }

        // Admin in-app notifications for failure visibility
        await NotifyAdminsOnFailureAsync(evt.UserId, fileName, stepText, evt.ErrorMessage, cancellationToken).ConfigureAwait(false);
    }

    private async Task NotifyAdminsOnFailureAsync(
        Guid uploaderId,
        string fileName,
        string stepText,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            var admins = await _userRepo.GetAdminUsersAsync(cancellationToken).ConfigureAwait(false);

            foreach (var admin in admins)
            {
                // Skip the uploader if they're also an admin (already notified above)
                if (admin.Id == uploaderId) continue;

                var notification = new Notification(
                    id: Guid.NewGuid(),
                    userId: admin.Id,
                    type: NotificationType.ProcessingJobFailed,
                    severity: NotificationSeverity.Warning,
                    title: "PDF Processing Failed",
                    message: $"'{fileName}' failed{stepText}: {errorMessage}",
                    link: "/admin/knowledge-base/queue"
                );
                await _notificationRepo.AddAsync(notification, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("Job failed admin notifications created for {Count} admins", admins.Count);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create admin notifications for job failure");
        }
#pragma warning restore CA1031
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1)
            return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        return $"{duration.TotalSeconds:F0}s";
    }

    private static string TruncateForPush(string? message, int maxLength = 120)
    {
        if (string.IsNullOrEmpty(message)) return "Unknown error";
        if (message.Length <= maxLength) return message;
        return string.Concat(message.AsSpan(0, maxLength - 3), "...");
    }
}
