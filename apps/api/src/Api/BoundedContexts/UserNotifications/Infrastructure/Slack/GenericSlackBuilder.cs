using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Slack;

/// <summary>
/// Fallback builder for notification types without a specific Block Kit builder.
/// Produces a simple header + section layout, with an optional deep link button.
/// </summary>
internal sealed class GenericSlackBuilder : ISlackMessageBuilder
{
    private readonly string _frontendBaseUrl;

    public GenericSlackBuilder(IConfiguration configuration)
    {
#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback value
        _frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? "https://meepleai.app";
#pragma warning restore S1075
    }

    /// <summary>
    /// Always returns false — this builder is used as the fallback by the factory,
    /// never selected via CanHandle.
    /// </summary>
    public bool CanHandle(NotificationType type) => false;

    public object BuildMessage(INotificationPayload payload, string? deepLinkPath)
    {
        var generic = payload as GenericPayload;
        var title = generic?.Title ?? "MeepleAI Notification";
        var body = generic?.Body ?? "You have a new notification.";

        var blocks = new List<object>
        {
            new
            {
                type = "header",
                text = new { type = "plain_text", text = title, emoji = true }
            },
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text = body }
            }
        };

        if (!string.IsNullOrEmpty(deepLinkPath))
        {
            var url = $"{_frontendBaseUrl.TrimEnd('/')}{deepLinkPath}";
            blocks.Add(new
            {
                type = "actions",
                elements = new object[]
                {
                    new
                    {
                        type = "button",
                        text = new { type = "plain_text", text = "\ud83d\udd17 Apri in MeepleAI", emoji = true },
                        action_id = "open_meepleai",
                        url
                    }
                }
            });
        }

        return new { blocks };
    }
}
