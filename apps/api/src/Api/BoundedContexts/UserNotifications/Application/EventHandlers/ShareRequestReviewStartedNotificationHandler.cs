using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.Constants;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Handles ShareRequestReviewStartedEvent to send notifications when an admin starts reviewing a share request.
/// Dispatches via NotificationDispatcher for multi-channel delivery (in-app, email, Slack).
/// Issue #3668: Phase 7 - Game proposal lifecycle notifications.
/// </summary>
internal sealed class ShareRequestReviewStartedNotificationHandler
    : DomainEventHandlerBase<ShareRequestReviewStartedEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly IUserRepository _userRepository;
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IPrivateGameRepository _privateGameRepository;

    public ShareRequestReviewStartedNotificationHandler(
        MeepleAiDbContext dbContext,
        INotificationDispatcher dispatcher,
        IUserRepository userRepository,
        IShareRequestRepository shareRequestRepository,
        ISharedGameRepository sharedGameRepository,
        IPrivateGameRepository privateGameRepository,
        ILogger<ShareRequestReviewStartedNotificationHandler> logger)
        : base(dbContext, logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _privateGameRepository = privateGameRepository ?? throw new ArgumentNullException(nameof(privateGameRepository));
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

        var game = await ShareRequestNotificationGame.FindAsync(
            shareRequest, _sharedGameRepository, _privateGameRepository, cancellationToken).ConfigureAwait(false);
        if (game == null)
        {
            Logger.LogWarning(
                "Game of share request {ShareRequestId} not found for notification",
                domainEvent.ShareRequestId);
            return;
        }

        await _dispatcher.DispatchAsync(new NotificationMessage
        {
            Type = NotificationType.GameProposalInReview,
            RecipientUserId = shareRequest.UserId,
            Payload = new ShareRequestPayload(
                domainEvent.ShareRequestId,
                user.DisplayName,
                game.Title,
                game.ImageUrl),
            DeepLinkPath = NotificationRoutes.ContributionRequest(domainEvent.ShareRequestId),
            // Issue #1937 / CF-1: propagate event id for dispatcher dedup
            SourceEventId = domainEvent.EventId
        }, cancellationToken).ConfigureAwait(false);

        Logger.LogInformation(
            "Dispatched notification for share request {ShareRequestId} review started by admin {AdminId}",
            domainEvent.ShareRequestId,
            domainEvent.AdminId);
    }

    protected override Guid? GetUserId(ShareRequestReviewStartedEvent domainEvent) => null;

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
