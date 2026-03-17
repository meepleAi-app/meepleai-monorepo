using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Slack;

/// <summary>
/// Block Kit builder for badge earned notifications.
/// Produces a celebratory header with badge name and description.
/// </summary>
internal sealed class BadgeSlackBuilder : ISlackMessageBuilder
{
    public bool CanHandle(NotificationType type)
    {
        return type == NotificationType.BadgeEarned;
    }

    public object BuildMessage(INotificationPayload payload, string? deepLinkPath)
    {
        if (payload is not BadgePayload badge)
        {
            throw new ArgumentException($"Expected {nameof(BadgePayload)} but received {payload.GetType().Name}", nameof(payload));
        }

        var blocks = new List<object>
        {
            new
            {
                type = "header",
                text = new { type = "plain_text", text = $"\ud83c\udfc6 Badge sbloccato: {badge.BadgeName}", emoji = true }
            },
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text = badge.Description }
            }
        };

        return new { blocks };
    }
}
