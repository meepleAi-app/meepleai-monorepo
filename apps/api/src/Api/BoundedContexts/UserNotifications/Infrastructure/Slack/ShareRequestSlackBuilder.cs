using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.SharedKernel.Services;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Slack;

/// <summary>
/// Block Kit builder for share request notifications (created, approved, rejected).
/// Produces interactive Approve/Reject buttons with block_id format: sr:{ShareRequestId}:{unix_timestamp}.
/// </summary>
internal sealed class ShareRequestSlackBuilder : ISlackMessageBuilder
{
    private readonly IPublicBaseUrlProvider _publicBaseUrl;
    private readonly TimeProvider _timeProvider;

    public ShareRequestSlackBuilder(IPublicBaseUrlProvider publicBaseUrl, TimeProvider timeProvider)
    {
        _publicBaseUrl = publicBaseUrl;
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

        var (headerEmoji, headerText) = sr.Status.ToLowerInvariant() switch
        {
            "created" => ("\ud83d\udce5", "Nuova Share Request"),
            "approved" => ("\u2705", "Approvata"),
            "rejected" => ("\u274c", "Rifiutata"),
            var unknown => throw new ArgumentException(
                $"Unrecognised ShareRequest status: '{unknown}'", nameof(payload))
        };

        var blocks = new List<object>
        {
            new
            {
                type = "header",
                text = new { type = "plain_text", text = $"{headerEmoji} {headerText}", emoji = true }
            },
            BuildSectionBlock(sr)
        };

        if (sr.Status.Equals("created", StringComparison.OrdinalIgnoreCase))
        {
            var timestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
            var blockId = $"sr:{sr.ShareRequestId}:{timestamp}";
            var safeDeepLinkPath = SlackDeepLinkValidator.Validate(deepLinkPath);
            var deepLink = safeDeepLinkPath is not null
                ? $"{_publicBaseUrl.GetBaseUrl()}{safeDeepLinkPath}"
                : $"{_publicBaseUrl.GetBaseUrl()}/share-requests/{sr.ShareRequestId}";

            blocks.Add(new
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
            });
        }

        return new { blocks };
    }

    private static object BuildSectionBlock(ShareRequestPayload sr)
    {
        var text = sr.Status.ToLowerInvariant() switch
        {
            "approved" => $"\u2705 La tua richiesta di condivisione per *{sr.GameTitle}* è stata approvata da {sr.RequesterName}.",
            "rejected" => $"\u274c La tua richiesta di condivisione per *{sr.GameTitle}* è stata rifiutata da {sr.RequesterName}.",
            _ => $"\ud83c\udfb2 *Gioco*: {sr.GameTitle}\n\ud83d\udc64 *Richiedente*: {sr.RequesterName}"
        };

        var safeImageUrl = sr.GameImageUrl is not null
            && sr.GameImageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? sr.GameImageUrl
            : null;

        if (safeImageUrl is not null)
        {
            return new
            {
                type = "section",
                text = new { type = "mrkdwn", text },
                accessory = new
                {
                    type = "image",
                    image_url = safeImageUrl,
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
