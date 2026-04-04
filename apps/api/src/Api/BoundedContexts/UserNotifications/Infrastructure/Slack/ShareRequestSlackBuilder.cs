using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Slack;

/// <summary>
/// Block Kit builder for share request notifications (created, approved, rejected).
/// Produces interactive Approve/Reject buttons with block_id format: sr:{ShareRequestId}:{unix_timestamp}.
/// </summary>
internal sealed class ShareRequestSlackBuilder : ISlackMessageBuilder
{
    private readonly string _frontendBaseUrl;
    private readonly TimeProvider _timeProvider;

    public ShareRequestSlackBuilder(IConfiguration configuration, TimeProvider timeProvider)
    {
#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback value
        _frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? "https://meepleai.app";
#pragma warning restore S1075
        _timeProvider = timeProvider;
    }

    public bool CanHandle(NotificationType type)
    {
        return type == NotificationType.ShareRequestCreated
            || type == NotificationType.ShareRequestApproved
            || type == NotificationType.ShareRequestRejected;
    }

    public object BuildMessage(INotificationPayload payload, string? deepLinkPath)
    {
        if (payload is not ShareRequestPayload sr)
        {
            throw new ArgumentException($"Expected {nameof(ShareRequestPayload)} but received {payload.GetType().Name}", nameof(payload));
        }

        var timestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var blockId = $"sr:{sr.ShareRequestId}:{timestamp}";
        var deepLink = $"{_frontendBaseUrl.TrimEnd('/')}/share-requests/{sr.ShareRequestId}";

        var blocks = new List<object>
        {
            new
            {
                type = "header",
                text = new { type = "plain_text", text = "\ud83d\udce5 Nuova Share Request", emoji = true }
            },
            BuildSectionBlock(sr),
            new
            {
                type = "actions",
                block_id = blockId,
                elements = new object[]
                {
                    new
                    {
                        type = "button",
                        text = new { type = "plain_text", text = "\u2705 Approva", emoji = true },
                        style = "primary",
                        action_id = "share_request_approve",
                        value = sr.ShareRequestId.ToString()
                    },
                    new
                    {
                        type = "button",
                        text = new { type = "plain_text", text = "\u274c Rifiuta", emoji = true },
                        style = "danger",
                        action_id = "share_request_reject",
                        value = sr.ShareRequestId.ToString()
                    },
                    new
                    {
                        type = "button",
                        text = new { type = "plain_text", text = "\ud83d\udd17 Apri in MeepleAI", emoji = true },
                        action_id = "open_meepleai",
                        url = deepLink
                    }
                }
            }
        };

        return new { blocks };
    }

    private static object BuildSectionBlock(ShareRequestPayload sr)
    {
        var text = $"*{sr.RequesterName}* vuole condividere il regolamento di *{sr.GameTitle}* con te.";

        if (!string.IsNullOrEmpty(sr.GameImageUrl))
        {
            return new
            {
                type = "section",
                text = new { type = "mrkdwn", text },
                accessory = new
                {
                    type = "image",
                    image_url = sr.GameImageUrl,
                    alt_text = sr.GameTitle
                }
            };
        }

        return new
        {
            type = "section",
            text = new { type = "mrkdwn", text }
        };
    }
}
