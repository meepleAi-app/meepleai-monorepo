using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Handles ShareRequestRejectedEvent to send notifications when a share request is rejected.
/// Creates both in-app notifications and email notifications with rejection reason.
/// </summary>
internal sealed class ShareRequestRejectedNotificationHandler
    : DomainEventHandlerBase<ShareRequestRejectedEvent>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IEmailService _emailService;

    public ShareRequestRejectedNotificationHandler(
        MeepleAiDbContext dbContext,
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        IShareRequestRepository shareRequestRepository,
        ISharedGameRepository sharedGameRepository,
        IEmailService emailService,
        ILogger<ShareRequestRejectedNotificationHandler> logger)
        : base(dbContext, logger)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
    }

    protected override async Task HandleEventAsync(
        ShareRequestRejectedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        // Get share request to extract user and game details
        var shareRequest = await _shareRequestRepository.GetByIdAsync(
            domainEvent.ShareRequestId, cancellationToken).ConfigureAwait(false);
        if (shareRequest == null)
        {
            Logger.LogWarning(
                "ShareRequest {ShareRequestId} not found for rejection notification",
                domainEvent.ShareRequestId);
            return;
        }

        var user = await _userRepository.GetByIdAsync(shareRequest.UserId, cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            Logger.LogWarning(
                "User {UserId} not found for share request rejection notification",
                shareRequest.UserId);
            return;
        }

        // Get source game to retrieve title
        var sourceGame = await _sharedGameRepository.GetByIdAsync(
            shareRequest.SourceGameId, cancellationToken).ConfigureAwait(false);
        if (sourceGame == null)
        {
            Logger.LogWarning(
                "Source game {SourceGameId} not found for share request notification",
                shareRequest.SourceGameId);
            return;
        }

        var gameTitle = sourceGame.Title;

        // Create in-app notification
        var notification = new Notification(
            id: Guid.NewGuid(),
            userId: shareRequest.UserId,
            type: NotificationType.ShareRequestRejected,
            severity: NotificationSeverity.Warning,
            title: "Share Request Not Approved",
            message: $"Your request to share \"{gameTitle}\" was not approved. Reason: {domainEvent.Reason}",
            link: $"/contributions/requests/{domainEvent.ShareRequestId}",
            metadata: JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["shareRequestId"] = domainEvent.ShareRequestId,
                ["gameTitle"] = gameTitle,
                ["reason"] = domainEvent.Reason
            }));

        await _notificationRepository.AddAsync(notification, cancellationToken).ConfigureAwait(false);

        Logger.LogInformation(
            "Created in-app notification for rejected share request {ShareRequestId}",
            domainEvent.ShareRequestId);

        // Send email notification with detailed feedback (best-effort)
        try
        {
            await _emailService.SendShareRequestRejectedEmailAsync(
                user.Email,
                user.DisplayName,
                gameTitle,
                domainEvent.Reason,
                cancellationToken).ConfigureAwait(false);

            Logger.LogInformation(
                "Sent share request rejected email to user {UserId} for game {GameTitle}",
                shareRequest.UserId,
                gameTitle);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // Log error but don't fail handler - email is non-critical
            Logger.LogError(
                ex,
                "Failed to send share request rejected email to user {UserId}",
                shareRequest.UserId);
        }
#pragma warning restore CA1031
    }

    protected override Guid? GetUserId(ShareRequestRejectedEvent domainEvent)
    {
        // Note: We need to get userId from share request in HandleEventAsync
        // Return null here as we don't have direct access to userId in the event
        return null;
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(ShareRequestRejectedEvent domainEvent)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ShareRequestId"] = domainEvent.ShareRequestId,
            ["AdminId"] = domainEvent.AdminId,
            ["Reason"] = domainEvent.Reason,
            ["Action"] = "ShareRequestRejectedNotification"
        };
    }
}
