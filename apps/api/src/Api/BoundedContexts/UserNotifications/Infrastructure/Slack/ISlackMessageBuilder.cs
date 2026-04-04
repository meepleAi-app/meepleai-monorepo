using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Slack;

internal interface ISlackMessageBuilder
{
    bool CanHandle(NotificationType type);
    object BuildMessage(INotificationPayload payload, string? deepLinkPath);
}
