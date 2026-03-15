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
/// Handles ShareRequestApprovedEvent to send celebratory notifications when a share request is approved.
/// Dispatches via NotificationDispatcher for multi-channel delivery (in-app, email, Slack).
/// Issue #3668: Phase 7 - Distinguishes MergeKnowledgeBase from ApproveAsNew/Variant.
/// </summary>
internal sealed class ShareRequestApprovedNotificationHandler
    : DomainEventHandlerBase<ShareRequestApprovedEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly IUserRepository _userRepository;
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly ISharedGameRepository _sharedGameRepository;

    public ShareRequestApprovedNotificationHandler(
        MeepleAiDbContext dbContext,
        INotificationDispatcher dispatcher,
        IUserRepository userRepository,
        IShareRequestRepository shareRequestRepository,
        ISharedGameRepository sharedGameRepository,
        ILogger<ShareRequestApprovedNotificationHandler> logger)
        : base(dbContext, logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
    }

    protected override async Task HandleEventAsync(
        ShareRequestApprovedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        // Get share request to extract user and game details
        var shareRequest = await _shareRequestRepository.GetByIdAsync(
            domainEvent.ShareRequestId, cancellationToken).ConfigureAwait(false);
        if (shareRequest == null)
        {
            Logger.LogWarning(
                "ShareRequest {ShareRequestId} not found for approval notification",
                domainEvent.ShareRequestId);
            return;
        }

        var user = await _userRepository.GetByIdAsync(shareRequest.UserId, cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            Logger.LogWarning(
                "User {UserId} not found for share request approval notification",
                shareRequest.UserId);
            return;
        }

        if (!domainEvent.TargetSharedGameId.HasValue)
        {
            Logger.LogWarning(
                "TargetSharedGameId is null for approved share request {ShareRequestId}",
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

        // ISSUE-3668: Determine if this is a MergeKnowledgeBase approval
        var targetGame = await _sharedGameRepository.GetByIdAsync(
            domainEvent.TargetSharedGameId.Value, cancellationToken).ConfigureAwait(false);
        if (targetGame == null)
        {
            Logger.LogWarning(
                "Target game {TargetSharedGameId} not found for share request notification",
                domainEvent.TargetSharedGameId.Value);
            return;
        }

        var isKbMerge = targetGame.CreatedBy != shareRequest.UserId;
        var targetSharedGameId = domainEvent.TargetSharedGameId.Value;

        var notificationType = isKbMerge
            ? NotificationType.GameProposalKbMerged
            : NotificationType.ShareRequestApproved;

        await _dispatcher.DispatchAsync(new NotificationMessage
        {
            Type = notificationType,
            RecipientUserId = shareRequest.UserId,
            Payload = new ShareRequestPayload(
                domainEvent.ShareRequestId,
                user.DisplayName,
                gameTitle,
                sourceGame.ImageUrl),
            DeepLinkPath = $"/shared-games/{targetSharedGameId}"
        }, cancellationToken).ConfigureAwait(false);

        Logger.LogInformation(
            "Dispatched notification for approved share request {ShareRequestId} (type: {NotificationType})",
            domainEvent.ShareRequestId,
            isKbMerge ? "KbMerged" : "NewGame");
    }

    protected override Guid? GetUserId(ShareRequestApprovedEvent domainEvent) => null;

    protected override Dictionary<string, object?>? GetAuditMetadata(ShareRequestApprovedEvent domainEvent)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ShareRequestId"] = domainEvent.ShareRequestId,
            ["AdminId"] = domainEvent.AdminId,
            ["TargetSharedGameId"] = domainEvent.TargetSharedGameId,
            ["Action"] = "ShareRequestApprovedNotification"
        };
    }
}
