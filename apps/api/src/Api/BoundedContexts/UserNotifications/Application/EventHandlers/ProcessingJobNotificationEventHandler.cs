using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using MediatR;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Handles ProcessingJob domain events to send multi-channel notifications via NotificationDispatcher.
/// Notifies the uploader when their PDF processing job completes or fails.
/// Issue #4736: Processing notifications - in-app, email, push.
/// </summary>
internal class ProcessingJobNotificationEventHandler :
    INotificationHandler<JobCompletedEvent>,
    INotificationHandler<JobFailedEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly IPdfDocumentRepository _pdfRepo;
    private readonly IUserRepository _userRepo;
    private readonly ILogger<ProcessingJobNotificationEventHandler> _logger;

    public ProcessingJobNotificationEventHandler(
        INotificationDispatcher dispatcher,
        IPdfDocumentRepository pdfRepo,
        IUserRepository userRepo,
        ILogger<ProcessingJobNotificationEventHandler> logger)
    {
        _dispatcher = dispatcher;
        _pdfRepo = pdfRepo;
        _userRepo = userRepo;
        _logger = logger;
    }

    public async Task Handle(JobCompletedEvent evt, CancellationToken cancellationToken)
    {
        var pdfDoc = await _pdfRepo.GetByIdAsync(evt.PdfDocumentId, cancellationToken).ConfigureAwait(false);
        if (pdfDoc == null)
        {
            _logger.LogWarning("PDF {PdfId} not found for job completed notification", evt.PdfDocumentId);
            return;
        }

        var fileName = pdfDoc.FileName.Value;
        var durationText = FormatDuration(evt.TotalDuration);

        await _dispatcher.DispatchAsync(new NotificationMessage
        {
            Type = NotificationType.ProcessingJobCompleted,
            RecipientUserId = evt.UserId,
            Payload = new PdfProcessingPayload(
                evt.PdfDocumentId,
                fileName,
                $"Completed in {durationText}"),
            DeepLinkPath = "/admin/knowledge-base/queue"
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Dispatched job completed notification for user {UserId}, job {JobId}", evt.UserId, evt.JobId);
    }

    public async Task Handle(JobFailedEvent evt, CancellationToken cancellationToken)
    {
        var pdfDoc = await _pdfRepo.GetByIdAsync(evt.PdfDocumentId, cancellationToken).ConfigureAwait(false);
        if (pdfDoc == null)
        {
            _logger.LogWarning("PDF {PdfId} not found for job failed notification", evt.PdfDocumentId);
            return;
        }

        var fileName = pdfDoc.FileName.Value;
        var stepText = evt.FailedAtStep != null ? $" at {evt.FailedAtStep}" : "";

        // Notify the uploader
        await _dispatcher.DispatchAsync(new NotificationMessage
        {
            Type = NotificationType.ProcessingJobFailed,
            RecipientUserId = evt.UserId,
            Payload = new PdfProcessingPayload(
                evt.PdfDocumentId,
                fileName,
                $"Failed{stepText}: {evt.ErrorMessage}"),
            DeepLinkPath = "/admin/knowledge-base/queue"
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogWarning("Dispatched job failed notification for user {UserId}, job {JobId}", evt.UserId, evt.JobId);

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

                await _dispatcher.DispatchAsync(new NotificationMessage
                {
                    Type = NotificationType.ProcessingJobFailed,
                    RecipientUserId = admin.Id,
                    Payload = new PdfProcessingPayload(
                        Guid.Empty,
                        fileName,
                        $"Failed{stepText}: {errorMessage}"),
                    DeepLinkPath = "/admin/knowledge-base/queue"
                }, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("Dispatched job failed admin notifications for {Count} admins", admins.Count);
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
}
