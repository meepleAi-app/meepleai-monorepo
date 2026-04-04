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
/// Handles SharedGameSubmittedForApprovalEvent to notify admins when a game is submitted for approval.
/// Dispatches via NotificationDispatcher for multi-channel delivery (in-app, email, Slack).
/// Issue #4159: Backend - Approval Workflow Extension
/// </summary>
internal sealed class SharedGameSubmittedForApprovalNotificationHandler
    : DomainEventHandlerBase<SharedGameSubmittedForApprovalEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly IUserRepository _userRepository;
    private readonly ISharedGameRepository _sharedGameRepository;

    public SharedGameSubmittedForApprovalNotificationHandler(
        MeepleAiDbContext dbContext,
        INotificationDispatcher dispatcher,
        IUserRepository userRepository,
        ISharedGameRepository sharedGameRepository,
        ILogger<SharedGameSubmittedForApprovalNotificationHandler> logger)
        : base(dbContext, logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
    }

    protected override async Task HandleEventAsync(
        SharedGameSubmittedForApprovalEvent domainEvent,
        CancellationToken cancellationToken)
    {
        // Get submitter details
        var submitter = await _userRepository.GetByIdAsync(domainEvent.SubmittedBy, cancellationToken).ConfigureAwait(false);
        if (submitter == null)
        {
            Logger.LogWarning(
                "Submitter user {UserId} not found for shared game approval notification",
                domainEvent.SubmittedBy);
            return;
        }

        // Get game details
        var game = await _sharedGameRepository.GetByIdAsync(domainEvent.GameId, cancellationToken).ConfigureAwait(false);
        if (game == null)
        {
            Logger.LogWarning(
                "Shared game {GameId} not found for approval notification",
                domainEvent.GameId);
            return;
        }

        // Get all admin users to notify
        var adminDomainEntities = await _userRepository.GetAdminUsersAsync(cancellationToken).ConfigureAwait(false);

        if (adminDomainEntities.Count == 0)
        {
            Logger.LogWarning(
                "No admin users found to notify for shared game {GameId} submission",
                domainEvent.GameId);
            return;
        }

        Logger.LogInformation(
            "Notifying {AdminCount} admins about shared game {GameId} submission by {SubmitterName}",
            adminDomainEntities.Count,
            domainEvent.GameId,
            submitter.DisplayName);

        // Dispatch notification for each admin
        foreach (var admin in adminDomainEntities)
        {
            await _dispatcher.DispatchAsync(new NotificationMessage
            {
                Type = NotificationType.AdminSharedGameSubmitted,
                RecipientUserId = admin.Id,
                Payload = new GenericPayload(
                    "New Game Submitted for Approval",
                    $"{submitter.DisplayName} submitted \"{game.Title}\" for approval."),
                DeepLinkPath = $"/admin/approval-queue?gameId={domainEvent.GameId}"
            }, cancellationToken).ConfigureAwait(false);
        }

        Logger.LogInformation(
            "Dispatched {Count} admin notifications for shared game {GameTitle} submission",
            adminDomainEntities.Count,
            game.Title);
    }

    protected override Guid? GetUserId(SharedGameSubmittedForApprovalEvent domainEvent) => domainEvent.SubmittedBy;

    protected override Dictionary<string, object?>? GetAuditMetadata(SharedGameSubmittedForApprovalEvent domainEvent)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["GameId"] = domainEvent.GameId,
            ["SubmittedBy"] = domainEvent.SubmittedBy,
            ["Action"] = "SharedGameSubmittedForApprovalNotification"
        };
    }
}
