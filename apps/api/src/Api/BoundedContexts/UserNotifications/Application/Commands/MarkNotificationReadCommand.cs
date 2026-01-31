using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to mark a single notification as read.
/// </summary>
internal record MarkNotificationReadCommand(
    Guid NotificationId,
    Guid UserId
) : ICommand<bool>;
