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
/// Handles ShareRequestCreatedEvent to send notifications when a share request is created.
/// Creates both in-app notifications and email notifications for the user.
/// </summary>
internal sealed class ShareRequestCreatedNotificationHandler
    : DomainEventHandlerBase<ShareRequestCreatedEvent>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IEmailService _emailService;

    public ShareRequestCreatedNotificationHandler(
        MeepleAiDbContext dbContext,
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        IShareRequestRepository shareRequestRepository,
        ISharedGameRepository sharedGameRepository,
        IEmailService emailService,
        ILogger<ShareRequestCreatedNotificationHandler> logger)
        : base(dbContext, logger)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
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
        var contributionType = domainEvent.ContributionType == SharedGameCatalog.Domain.ValueObjects.ContributionType.NewGame
            ? "new game"
            : "additional content";

        // Create in-app notification
        var notification = new Notification(
            id: Guid.NewGuid(),
            userId: domainEvent.UserId,
            type: NotificationType.ShareRequestCreated,
            severity: NotificationSeverity.Info,
            title: "Share Request Submitted",
            message: $"Your request to share \"{gameTitle}\" has been submitted for review.",
            link: $"/contributions/requests/{domainEvent.ShareRequestId}",
            metadata: JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["shareRequestId"] = domainEvent.ShareRequestId,
                ["gameTitle"] = gameTitle,
                ["contributionType"] = contributionType
            }));

        await _notificationRepository.AddAsync(notification, cancellationToken).ConfigureAwait(false);

        Logger.LogInformation(
            "Created in-app notification for share request {ShareRequestId} created by user {UserId}",
            domainEvent.ShareRequestId,
            domainEvent.UserId);

        // Send email notification (best-effort, don't fail handler)
        try
        {
            await _emailService.SendShareRequestCreatedEmailAsync(
                user.Email,
                user.DisplayName,
                gameTitle,
                contributionType,
                domainEvent.ShareRequestId,
                cancellationToken).ConfigureAwait(false);

            Logger.LogInformation(
                "Sent share request created email to user {UserId} for game {GameTitle}",
                domainEvent.UserId,
                gameTitle);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // Log error but don't fail handler - email is non-critical
            Logger.LogError(
                ex,
                "Failed to send share request created email to user {UserId}",
                domainEvent.UserId);
        }
#pragma warning restore CA1031
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
