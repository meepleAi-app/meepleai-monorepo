using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to remove a push notification subscription for a user.
/// Issue #4416: Push notifications via Service Worker.
/// </summary>
internal record UnsubscribePushNotificationsCommand(Guid UserId) : ICommand;
