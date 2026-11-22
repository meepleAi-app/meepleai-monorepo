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
/// Handles ShareRequestRejectedEvent to send notifications when a share request is rejected.
/// Dispatches via NotificationDispatcher for multi-channel delivery (in-app, email, Slack).
/// </summary>
internal sealed class ShareRequestRejectedNotificationHandler
    : DomainEventHandlerBase<ShareRequestRejectedEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly IUserRepository _userRepository;
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IPrivateGameRepository _privateGameRepository;

    public ShareRequestRejectedNotificationHandler(
        MeepleAiDbContext dbContext,
        INotificationDispatcher dispatcher,
        IUserRepository userRepository,
        IShareRequestRepository shareRequestRepository,
        ISharedGameRepository sharedGameRepository,
        IPrivateGameRepository privateGameRepository,
        ILogger<ShareRequestRejectedNotificationHandler> logger)
        : base(dbContext, logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _privateGameRepository = privateGameRepository ?? throw new ArgumentNullException(nameof(privateGameRepository));
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
            Type = NotificationType.ShareRequestRejected,
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
            "Dispatched notification for rejected share request {ShareRequestId}",
            domainEvent.ShareRequestId);
    }

    protected override Guid? GetUserId(ShareRequestRejectedEvent domainEvent) => null;

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
