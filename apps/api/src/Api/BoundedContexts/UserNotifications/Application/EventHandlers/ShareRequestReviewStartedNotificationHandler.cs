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
/// Handles ShareRequestReviewStartedEvent to send notifications when an admin starts reviewing a share request.
/// Creates both in-app notifications and email notifications for the user.
/// Issue #3668: Phase 7 - Game proposal lifecycle notifications.
/// </summary>
internal sealed class ShareRequestReviewStartedNotificationHandler
    : DomainEventHandlerBase<ShareRequestReviewStartedEvent>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IEmailService _emailService;

    public ShareRequestReviewStartedNotificationHandler(
        MeepleAiDbContext dbContext,
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        IShareRequestRepository shareRequestRepository,
        ISharedGameRepository sharedGameRepository,
        IEmailService emailService,
        ILogger<ShareRequestReviewStartedNotificationHandler> logger)
        : base(dbContext, logger)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
    }

    protected override async Task HandleEventAsync(
        ShareRequestReviewStartedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        // Get share request to find user ID
        var shareRequest = await _shareRequestRepository.GetByIdAsync(
            domainEvent.ShareRequestId, cancellationToken).ConfigureAwait(false);
        if (shareRequest == null)
        {
            Logger.LogWarning(
                "ShareRequest {ShareRequestId} not found for review started notification",
                domainEvent.ShareRequestId);
            return;
        }

        // Get user details
        var user = await _userRepository.GetByIdAsync(shareRequest.UserId, cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            Logger.LogWarning(
                "User {UserId} not found for share request review started notification",
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
            type: NotificationType.GameProposalInReview,
            severity: NotificationSeverity.Info,
            title: "Game Proposal Under Review",
            message: $"An admin has started reviewing your proposal for \"{gameTitle}\".",
            link: $"/contributions/requests/{domainEvent.ShareRequestId}",
            metadata: JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["shareRequestId"] = domainEvent.ShareRequestId,
                ["gameTitle"] = gameTitle,
                ["adminId"] = domainEvent.AdminId
            }));

        await _notificationRepository.AddAsync(notification, cancellationToken).ConfigureAwait(false);

        Logger.LogInformation(
            "Created in-app notification for share request {ShareRequestId} review started by admin {AdminId}",
            domainEvent.ShareRequestId,
            domainEvent.AdminId);

        // Send email notification (best-effort, don't fail handler)
        try
        {
            await _emailService.SendShareRequestReviewStartedEmailAsync(
                user.Email,
                user.DisplayName,
                gameTitle,
                domainEvent.ShareRequestId,
                cancellationToken).ConfigureAwait(false);

            Logger.LogInformation(
                "Sent review started email to user {UserId} for game {GameTitle}",
                shareRequest.UserId,
                gameTitle);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // Log error but don't fail handler - email is non-critical
            Logger.LogError(
                ex,
                "Failed to send review started email to user {UserId}",
                shareRequest.UserId);
        }
#pragma warning restore CA1031
    }

    protected override Guid? GetUserId(ShareRequestReviewStartedEvent domainEvent) => null; // UserId not on event, fetched from ShareRequest

    protected override Dictionary<string, object?>? GetAuditMetadata(ShareRequestReviewStartedEvent domainEvent)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ShareRequestId"] = domainEvent.ShareRequestId,
            ["AdminId"] = domainEvent.AdminId,
            ["Action"] = "ShareRequestReviewStartedNotification"
        };
    }
}
