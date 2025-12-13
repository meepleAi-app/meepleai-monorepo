using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Handler for MarkAllNotificationsReadCommand.
/// Bulk operation to mark all user notifications as read.
/// </summary>
public class MarkAllNotificationsReadCommandHandler : ICommandHandler<MarkAllNotificationsReadCommand, int>
{
    private readonly INotificationRepository _notificationRepository;

    public MarkAllNotificationsReadCommandHandler(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
    }

    public async Task<int> Handle(MarkAllNotificationsReadCommand command, CancellationToken cancellationToken)
    {
        return await _notificationRepository.MarkAllAsReadAsync(command.UserId, cancellationToken).ConfigureAwait(false);
    }
}
