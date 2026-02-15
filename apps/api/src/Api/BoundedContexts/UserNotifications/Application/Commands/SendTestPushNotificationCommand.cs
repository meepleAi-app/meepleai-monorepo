using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to send a test push notification to the user's subscribed browser.
/// Issue #4416: Push notification testing.
/// </summary>
internal record SendTestPushNotificationCommand(Guid UserId) : ICommand;
