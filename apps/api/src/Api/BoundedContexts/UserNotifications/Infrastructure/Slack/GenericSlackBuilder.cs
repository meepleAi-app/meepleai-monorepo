using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.SharedKernel.Services;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Slack;

/// <summary>
/// Fallback builder for notification types without a specific Block Kit builder.
/// Produces a simple header + section layout, with an optional deep link button.
/// </summary>
internal sealed class GenericSlackBuilder : ISlackMessageBuilder
{
    private readonly IPublicBaseUrlProvider _publicBaseUrl;

    public GenericSlackBuilder(IPublicBaseUrlProvider publicBaseUrl)
    {
        _publicBaseUrl = publicBaseUrl;
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

        var safeDeepLinkPath = SlackDeepLinkValidator.Validate(deepLinkPath);
        if (safeDeepLinkPath is not null)
        {
            var url = $"{_publicBaseUrl.GetBaseUrl()}{safeDeepLinkPath}";
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
