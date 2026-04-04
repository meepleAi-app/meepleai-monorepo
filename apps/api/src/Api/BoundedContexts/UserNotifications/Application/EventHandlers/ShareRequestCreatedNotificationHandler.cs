using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Handles ShareRequestCreatedEvent to send notifications when a share request is created.
/// Dispatches via NotificationDispatcher for multi-channel delivery (in-app, email, Slack).
/// </summary>
internal sealed class ShareRequestCreatedNotificationHandler
    : DomainEventHandlerBase<ShareRequestCreatedEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly IUserRepository _userRepository;
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly ISharedGameRepository _sharedGameRepository;

    public ShareRequestCreatedNotificationHandler(
        MeepleAiDbContext dbContext,
        INotificationDispatcher dispatcher,
        IUserRepository userRepository,
        IShareRequestRepository shareRequestRepository,
        ISharedGameRepository sharedGameRepository,
        ILogger<ShareRequestCreatedNotificationHandler> logger)
        : base(dbContext, logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
    }

    protected override async Task HandleEventAsync(
        ShareRequestCreatedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        // Get user and share request details
        var user = await _userRepository.GetByIdAsync(domainEvent.UserId, cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            Logger.LogWarning(
                "User {UserId} not found for share request created notification",
                domainEvent.UserId);
            return;
        }

        var shareRequest = await _shareRequestRepository.GetByIdAsync(
            domainEvent.ShareRequestId, cancellationToken).ConfigureAwait(false);
        if (shareRequest == null)
        {
            Logger.LogWarning(
                "ShareRequest {ShareRequestId} not found for notification",
                domainEvent.ShareRequestId);
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

        await _dispatcher.DispatchAsync(new NotificationMessage
        {
            Type = NotificationType.ShareRequestCreated,
            RecipientUserId = domainEvent.UserId,
            Payload = new ShareRequestPayload(
                domainEvent.ShareRequestId,
                user.DisplayName,
                gameTitle,
                sourceGame.ImageUrl),
            DeepLinkPath = $"/contributions/requests/{domainEvent.ShareRequestId}"
        }, cancellationToken).ConfigureAwait(false);

        Logger.LogInformation(
            "Dispatched notification for share request {ShareRequestId} created by user {UserId}",
            domainEvent.ShareRequestId,
            domainEvent.UserId);
    }

    protected override Guid? GetUserId(ShareRequestCreatedEvent domainEvent) => domainEvent.UserId;

    protected override Dictionary<string, object?>? GetAuditMetadata(ShareRequestCreatedEvent domainEvent)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ShareRequestId"] = domainEvent.ShareRequestId,
            ["UserId"] = domainEvent.UserId,
            ["Action"] = "ShareRequestCreatedNotification"
        };
    }
}
