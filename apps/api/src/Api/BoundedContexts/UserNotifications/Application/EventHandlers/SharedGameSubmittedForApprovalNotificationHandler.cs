using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Handles SharedGameSubmittedForApprovalEvent to notify admins when a game is submitted for approval.
/// Creates in-app notifications and email notifications for all admin users.
/// Issue #4159: Backend - Approval Workflow Extension
/// </summary>
internal sealed class SharedGameSubmittedForApprovalNotificationHandler
    : DomainEventHandlerBase<SharedGameSubmittedForApprovalEvent>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IEmailService _emailService;

    public SharedGameSubmittedForApprovalNotificationHandler(
        MeepleAiDbContext dbContext,
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        ISharedGameRepository sharedGameRepository,
        IEmailService emailService,
        ILogger<SharedGameSubmittedForApprovalNotificationHandler> logger)
        : base(dbContext, logger)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
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

        // Get all admin users to notify (both Admin and SuperAdmin roles)
        var adminDomainEntities = await _userRepository.GetAdminUsersAsync(cancellationToken).ConfigureAwait(false);

        var adminUsers = adminDomainEntities
            .Select(u => new { u.Id, Email = u.Email.Value, u.DisplayName })
            .ToList();

        if (adminUsers.Count == 0)
        {
            Logger.LogWarning(
                "No admin users found to notify for shared game {GameId} submission",
                domainEvent.GameId);
            return;
        }

        Logger.LogInformation(
            "Notifying {AdminCount} admins about shared game {GameId} submission by {SubmitterName}",
            adminUsers.Count,
            domainEvent.GameId,
            submitter.DisplayName);

        // Create in-app notification for each admin
        foreach (var admin in adminUsers)
        {
            var notification = new Notification(
                id: Guid.NewGuid(),
                userId: admin.Id,
                type: NotificationType.AdminSharedGameSubmitted,
                severity: NotificationSeverity.Info,
                title: "New Game Submitted for Approval",
                message: $"{submitter.DisplayName} submitted \"{game.Title}\" for approval.",
                link: $"/admin/approval-queue?gameId={domainEvent.GameId}",
                metadata: JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["gameId"] = domainEvent.GameId,
                    ["gameTitle"] = game.Title,
                    ["submittedBy"] = domainEvent.SubmittedBy,
                    ["submitterName"] = submitter.DisplayName
                }));

            await _notificationRepository.AddAsync(notification, cancellationToken).ConfigureAwait(false);

            Logger.LogInformation(
                "Created in-app notification for admin {AdminId} about game {GameTitle}",
                admin.Id,
                game.Title);

            // Send email notification (best-effort, don't fail handler)
            try
            {
                await _emailService.SendSharedGameSubmittedForApprovalEmailAsync(
                    admin.Email,
                    admin.DisplayName,
                    game.Title,
                    submitter.DisplayName,
                    domainEvent.GameId,
                    cancellationToken).ConfigureAwait(false);

                Logger.LogInformation(
                    "Sent approval notification email to admin {AdminEmail} for game {GameTitle}",
                    admin.Email,
                    game.Title);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                // Log error but don't fail handler - email is non-critical
                Logger.LogError(
                    ex,
                    "Failed to send approval email to admin {AdminEmail}",
                    admin.Email);
            }
#pragma warning restore CA1031
        }
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
