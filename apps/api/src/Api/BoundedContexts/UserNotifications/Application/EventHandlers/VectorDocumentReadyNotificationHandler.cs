using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Services;
using Api.SharedKernel.Application.IntegrationEvents;
using MediatR;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Handles VectorDocumentReadyIntegrationEvent to send multi-channel notifications
/// when a user's PDF knowledge base is indexed and ready.
/// Issue #5237: Decoupled from KnowledgeBase — notifications are now handled within
/// the UserNotifications bounded context via integration events.
/// </summary>
internal sealed class VectorDocumentReadyNotificationHandler
    : INotificationHandler<VectorDocumentReadyIntegrationEvent>
{
    private readonly INotificationRepository _notificationRepo;
    private readonly INotificationPreferencesRepository _preferencesRepo;
    private readonly IUserRepository _userRepo;
    private readonly IMediator _mediator;
    private readonly IPushNotificationService _pushService;
    private readonly ILogger<VectorDocumentReadyNotificationHandler> _logger;

    public VectorDocumentReadyNotificationHandler(
        INotificationRepository notificationRepo,
        INotificationPreferencesRepository preferencesRepo,
        IUserRepository userRepo,
        IMediator mediator,
        IPushNotificationService pushService,
        ILogger<VectorDocumentReadyNotificationHandler> logger)
    {
        _notificationRepo = notificationRepo;
        _preferencesRepo = preferencesRepo;
        _userRepo = userRepo;
        _mediator = mediator;
        _pushService = pushService;
        _logger = logger;
    }

    public async Task Handle(
        VectorDocumentReadyIntegrationEvent evt,
        CancellationToken cancellationToken)
    {
        var userId = evt.UploadedByUserId;

        // Fail-safe: if user has not configured notification preferences, skip silently
        var prefs = await _preferencesRepo.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (prefs == null)
        {
            _logger.LogWarning(
                "VectorDocumentReadyNotificationHandler: No notification preferences for user {UserId}, skipping notification",
                userId);
            return;
        }

        var fileName = evt.FileName;
        var agentLink = $"/library/games/{evt.GameId}/agent";
        var chunkCount = evt.ChunkCount;

        // In-App Notification
        if (prefs.InAppOnDocumentReady)
        {
            var notification = new Notification(
                id: Guid.NewGuid(),
                userId: userId,
                type: NotificationType.ProcessingJobCompleted,
                severity: NotificationSeverity.Success,
                title: "Knowledge Base pronta",
                message: $"'{fileName}' è stato indicizzato ({chunkCount} chunk). Puoi ora creare il tuo agente.",
                link: agentLink
            );
            await _notificationRepo.AddAsync(notification, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "KB ready in-app notification created for user {UserId}, game {GameId}, chunks: {ChunkCount}",
                userId,
                evt.GameId,
                chunkCount);
        }

        // Email Notification via Queue
        if (prefs.EmailOnDocumentReady)
        {
            var user = await _userRepo.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
            if (user != null)
            {
                try
                {
                    await _mediator.Send(new EnqueueEmailCommand(
                        UserId: userId,
                        To: user.Email,
                        Subject: $"La tua Knowledge Base è pronta: {fileName} - MeepleAI",
                        TemplateName: "kb_indexed",
                        UserName: user.DisplayName,
                        FileName: fileName,
                        DocumentUrl: agentLink
                    ), cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation(
                        "KB ready email enqueued for user {UserId}",
                        userId);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to enqueue KB ready email for user {UserId}", userId);
                }
#pragma warning restore CA1031
            }
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
                    "Knowledge Base pronta",
                    $"'{fileName}' è stato indicizzato ({chunkCount} chunk). Crea il tuo agente!",
                    agentLink,
                    cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send push notification for KB ready to user {UserId}", userId);
            }
#pragma warning restore CA1031
        }
    }
}
