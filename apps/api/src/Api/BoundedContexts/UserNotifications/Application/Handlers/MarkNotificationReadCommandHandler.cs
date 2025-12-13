using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Handler for MarkNotificationReadCommand.
/// Marks a single notification as read.
/// </summary>
public class MarkNotificationReadCommandHandler : ICommandHandler<MarkNotificationReadCommand, bool>
{
    private readonly INotificationRepository _notificationRepository;

    public MarkNotificationReadCommandHandler(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
    }

    public async Task<bool> Handle(MarkNotificationReadCommand command, CancellationToken cancellationToken)
    {
        var notification = await _notificationRepository.GetByIdAsync(command.NotificationId, cancellationToken).ConfigureAwait(false);

        if (notification == null)
        {
            return false; // Notification not found
        }

        // Verify ownership
        if (notification.UserId != command.UserId)
        {
            throw new UnauthorizedAccessException($"User {command.UserId} cannot mark notification {command.NotificationId} as read");
        }

        notification.MarkAsRead();
        await _notificationRepository.UpdateAsync(notification, cancellationToken).ConfigureAwait(false);

        return true;
    }
}
