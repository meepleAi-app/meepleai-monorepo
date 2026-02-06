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
/// Handles ShareRequestApprovedEvent to send celebratory notifications when a share request is approved.
/// Creates both in-app notifications and email notifications for the user.
/// Issue #3668: Phase 7 - Distinguishes MergeKnowledgeBase from ApproveAsNew/Variant.
/// </summary>
internal sealed class ShareRequestApprovedNotificationHandler
    : DomainEventHandlerBase<ShareRequestApprovedEvent>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IEmailService _emailService;

    public ShareRequestApprovedNotificationHandler(
        MeepleAiDbContext dbContext,
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        IShareRequestRepository shareRequestRepository,
        ISharedGameRepository sharedGameRepository,
        IEmailService emailService,
        ILogger<ShareRequestApprovedNotificationHandler> logger)
        : base(dbContext, logger)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
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
        // MergeKB = target game created by someone else (pre-existing game)
        // ApproveAsNew/Variant = target game created by proposal user (just created by approval)
        var targetGame = await _sharedGameRepository.GetByIdAsync(
            domainEvent.TargetSharedGameId.Value, cancellationToken).ConfigureAwait(false);
        if (targetGame == null)
        {
            Logger.LogWarning(
                "Target game {TargetSharedGameId} not found for share request notification",
                domainEvent.TargetSharedGameId.Value);
            return;
        }

        // If target game was created by a different user, it's a KB merge
        // If created by the proposal user, it's a new game (ApproveAsNew/Variant)
        var isKbMerge = targetGame.CreatedBy != shareRequest.UserId;

        // Create in-app notification (different type for KB merge vs new game)
        var targetSharedGameId = domainEvent.TargetSharedGameId.Value;
        var notification = isKbMerge
            ? CreateKbMergedNotification(shareRequest, domainEvent, gameTitle, targetSharedGameId)
            : CreateNewGameNotification(shareRequest, domainEvent, gameTitle, targetSharedGameId);

        await _notificationRepository.AddAsync(notification, cancellationToken).ConfigureAwait(false);

        Logger.LogInformation(
            "Created in-app notification for approved share request {ShareRequestId} (type: {NotificationType})",
            domainEvent.ShareRequestId,
            isKbMerge ? "KbMerged" : "NewGame");

        // Send appropriate email notification (best-effort, don't fail handler)
        try
        {
            if (isKbMerge)
            {
                await _emailService.SendShareRequestKbMergedEmailAsync(
                    user.Email,
                    user.DisplayName,
                    gameTitle,
                    domainEvent.TargetSharedGameId.Value,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _emailService.SendShareRequestApprovedEmailAsync(
                    user.Email,
                    user.DisplayName,
                    gameTitle,
                    domainEvent.TargetSharedGameId.Value,
                    shareRequest.UserId,
                    cancellationToken).ConfigureAwait(false);
            }

            Logger.LogInformation(
                "Sent share request {EmailType} email to user {UserId} for game {GameTitle}",
                isKbMerge ? "KB merged" : "approved",
                shareRequest.UserId,
                gameTitle);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // Log error but don't fail handler - email is non-critical
            Logger.LogError(
                ex,
                "Failed to send share request approved email to user {UserId}",
                shareRequest.UserId);
        }
#pragma warning restore CA1031
    }

    private Notification CreateKbMergedNotification(
        SharedGameCatalog.Domain.Entities.ShareRequest shareRequest,
        ShareRequestApprovedEvent domainEvent,
        string gameTitle,
        Guid targetSharedGameId)
    {
        return new Notification(
            id: Guid.NewGuid(),
            userId: shareRequest.UserId,
            type: NotificationType.GameProposalKbMerged,
            severity: NotificationSeverity.Success,
            title: "Game Proposal Merged! 🎉",
            message: $"Your knowledge base for \"{gameTitle}\" has been merged into the existing game in the catalog.",
            link: $"/shared-games/{targetSharedGameId}",
            metadata: JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["shareRequestId"] = domainEvent.ShareRequestId,
                ["sharedGameId"] = targetSharedGameId,
                ["gameTitle"] = gameTitle,
                ["approvalType"] = "KbMerge"
            }));
    }

    private Notification CreateNewGameNotification(
        SharedGameCatalog.Domain.Entities.ShareRequest shareRequest,
        ShareRequestApprovedEvent domainEvent,
        string gameTitle,
        Guid targetSharedGameId)
    {
        return new Notification(
            id: Guid.NewGuid(),
            userId: shareRequest.UserId,
            type: NotificationType.ShareRequestApproved,
            severity: NotificationSeverity.Success,
            title: "Contribution Approved! 🎉",
            message: $"Great news! Your contribution for \"{gameTitle}\" has been approved and is now available in the shared catalog.",
            link: $"/shared-games/{targetSharedGameId}",
            metadata: JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["shareRequestId"] = domainEvent.ShareRequestId,
                ["sharedGameId"] = targetSharedGameId,
                ["gameTitle"] = gameTitle,
                ["approvalType"] = "NewGame"
            }));
    }

    protected override Guid? GetUserId(ShareRequestApprovedEvent domainEvent)
    {
        // Note: We need to get userId from share request in HandleEventAsync
        // Return null here as we don't have direct access to userId in the event
        return null;
    }

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
