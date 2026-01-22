using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Event handler that creates admin notifications when new share requests are submitted.
/// ISSUE-2740: Admin alert system for share request management.
/// </summary>
internal sealed class NewShareRequestAdminAlertHandler : INotificationHandler<ShareRequestCreatedEvent>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<NewShareRequestAdminAlertHandler> _logger;

    public NewShareRequestAdminAlertHandler(
        INotificationRepository notificationRepository,
        MeepleAiDbContext dbContext,
        ILogger<NewShareRequestAdminAlertHandler> logger)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ShareRequestCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Get all admin users
            // NOTE: For now we query DbContext directly. Can be refactored to IUserRepository.GetByRoleAsync in future.
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

            // Create notification for each admin
            foreach (var adminId in adminUsers)
            {
                var adminNotification = new Notification(
                    id: Guid.NewGuid(),
                    userId: adminId,
                    type: NotificationType.AdminNewShareRequest,
                    severity: NotificationSeverity.Info,
                    title: "New Share Request",
                    message: "A new share request is waiting for review.",
                    link: $"/admin/share-requests/{notification.ShareRequestId}",
                    metadata: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        shareRequestId = notification.ShareRequestId,
                        contributionType = notification.ContributionType.ToString(),
                        userId = notification.UserId
                    }));

                await _notificationRepository.AddAsync(adminNotification, cancellationToken).ConfigureAwait(false);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Created {Count} admin notifications for share request {ShareRequestId}",
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
