using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Event handler that creates admin notifications when new share requests are submitted.
/// Dispatches via NotificationDispatcher for multi-channel delivery (in-app, email, Slack).
/// ISSUE-2740: Admin alert system for share request management.
/// </summary>
internal sealed class NewShareRequestAdminAlertHandler : INotificationHandler<ShareRequestCreatedEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<NewShareRequestAdminAlertHandler> _logger;

    public NewShareRequestAdminAlertHandler(
        INotificationDispatcher dispatcher,
        MeepleAiDbContext dbContext,
        ILogger<NewShareRequestAdminAlertHandler> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ShareRequestCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Get all admin users
            var adminUsers = await _dbContext.Set<UserEntity>()
                .AsNoTracking()
                .Where(u => u.Role == "admin")
                .Select(u => u.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (adminUsers.Count == 0)
            {
                _logger.LogWarning(
                    "No admin users found to notify for share request {ShareRequestId}",
                    notification.ShareRequestId);
                return;
            }

            // Dispatch notification for each admin
            foreach (var adminId in adminUsers)
            {
                await _dispatcher.DispatchAsync(new NotificationMessage
                {
                    Type = NotificationType.AdminNewShareRequest,
                    RecipientUserId = adminId,
                    Payload = new GenericPayload(
                        "New Share Request",
                        "A new share request is waiting for review."),
                    DeepLinkPath = $"/admin/share-requests/{notification.ShareRequestId}"
                }, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation(
                "Dispatched {Count} admin notifications for share request {ShareRequestId}",
                adminUsers.Count,
                notification.ShareRequestId);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create admin notifications for share request {ShareRequestId}",
                notification.ShareRequestId);
            // Don't rethrow - event handler failures should not block the main operation
        }
#pragma warning restore CA1031
    }
}
