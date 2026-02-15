using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to register a push notification subscription for a user.
/// Issue #4416: Push notifications via Service Worker.
/// </summary>
internal record SubscribePushNotificationsCommand(
    Guid UserId,
    string Endpoint,
    string P256dhKey,
    string AuthKey
) : ICommand;
