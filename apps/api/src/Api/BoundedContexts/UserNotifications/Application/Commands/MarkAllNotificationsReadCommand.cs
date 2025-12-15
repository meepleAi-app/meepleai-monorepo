using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to mark all notifications as read for a user.
/// Bulk operation for "clear all" functionality.
/// </summary>
internal record MarkAllNotificationsReadCommand(Guid UserId) : ICommand<int>;
