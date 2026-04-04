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
/// Handles ShareRequestChangesRequestedEvent to send notifications when changes are requested.
/// Dispatches via NotificationDispatcher for multi-channel delivery (in-app, email, Slack).
/// </summary>
internal sealed class ShareRequestChangesRequestedNotificationHandler
    : DomainEventHandlerBase<ShareRequestChangesRequestedEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly IUserRepository _userRepository;
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly ISharedGameRepository _sharedGameRepository;

    public ShareRequestChangesRequestedNotificationHandler(
        MeepleAiDbContext dbContext,
        INotificationDispatcher dispatcher,
        IUserRepository userRepository,
        IShareRequestRepository shareRequestRepository,
        ISharedGameRepository sharedGameRepository,
        ILogger<ShareRequestChangesRequestedNotificationHandler> logger)
        : base(dbContext, logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
    }

    protected override async Task HandleEventAsync(
        ShareRequestChangesRequestedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        // Get share request to extract user and game details
        var shareRequest = await _shareRequestRepository.GetByIdAsync(
            domainEvent.ShareRequestId, cancellationToken).ConfigureAwait(false);
        if (shareRequest == null)
        {
            Logger.LogWarning(
                "ShareRequest {ShareRequestId} not found for changes requested notification",
                domainEvent.ShareRequestId);
            return;
        }

        var user = await _userRepository.GetByIdAsync(shareRequest.UserId, cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            Logger.LogWarning(
                "User {UserId} not found for share request changes requested notification",
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

        await _dispatcher.DispatchAsync(new NotificationMessage
        {
            Type = NotificationType.ShareRequestChangesRequested,
            RecipientUserId = shareRequest.UserId,
            Payload = new ShareRequestPayload(
                domainEvent.ShareRequestId,
                user.DisplayName,
                gameTitle,
                sourceGame.ImageUrl),
            DeepLinkPath = $"/contributions/requests/{domainEvent.ShareRequestId}"
        }, cancellationToken).ConfigureAwait(false);

        Logger.LogInformation(
            "Dispatched notification for changes requested on share request {ShareRequestId}",
            domainEvent.ShareRequestId);
    }

    protected override Guid? GetUserId(ShareRequestChangesRequestedEvent domainEvent) => null;

    protected override Dictionary<string, object?>? GetAuditMetadata(ShareRequestChangesRequestedEvent domainEvent)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ShareRequestId"] = domainEvent.ShareRequestId,
            ["AdminId"] = domainEvent.AdminId,
            ["Feedback"] = domainEvent.Feedback,
            ["Action"] = "ShareRequestChangesRequestedNotification"
        };
    }
}
