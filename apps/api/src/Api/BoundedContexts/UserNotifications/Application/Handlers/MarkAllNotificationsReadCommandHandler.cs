using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Handler for MarkAllNotificationsReadCommand.
/// Bulk operation to mark all user notifications as read.
/// </summary>
public class MarkAllNotificationsReadCommandHandler : ICommandHandler<MarkAllNotificationsReadCommand, int>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly ILogger<MarkAllNotificationsReadCommandHandler> _logger;

    public MarkAllNotificationsReadCommandHandler(
        INotificationRepository notificationRepository,
        ILogger<MarkAllNotificationsReadCommandHandler> logger)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> Handle(MarkAllNotificationsReadCommand command, CancellationToken cancellationToken)
    {
        var count = await _notificationRepository.MarkAllAsReadAsync(command.UserId, cancellationToken).ConfigureAwait(false);

        if (count > 0)
        {
            _logger.LogInformation("Marked {Count} notifications as read for user {UserId}",
                count, command.UserId);
        }
        else
        {
            _logger.LogDebug("No unread notifications to mark for user {UserId}", command.UserId);
        }

        return count;
    }
}