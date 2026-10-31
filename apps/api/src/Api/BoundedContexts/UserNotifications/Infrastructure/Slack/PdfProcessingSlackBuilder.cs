using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.SharedKernel.Services;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Slack;

/// <summary>
/// Block Kit builder for PDF processing notifications (ready or failed).
/// Produces a status header with emoji and a link to the document in MeepleAI.
/// </summary>
internal sealed class PdfProcessingSlackBuilder : ISlackMessageBuilder
{
    private readonly IPublicBaseUrlProvider _publicBaseUrl;

    public PdfProcessingSlackBuilder(IPublicBaseUrlProvider publicBaseUrl)
    {
        _publicBaseUrl = publicBaseUrl;
    }

    public bool CanHandle(NotificationType type)
    {
        return type == NotificationType.DocumentReady
            || type == NotificationType.DocumentProcessingFailed;
    }

    public object BuildMessage(INotificationPayload payload, string? deepLinkPath)
    {
        if (payload is not PdfProcessingPayload pdf)
        {
            throw new ArgumentException($"Expected {nameof(PdfProcessingPayload)} but received {payload.GetType().Name}", nameof(payload));
        }

        var isSuccess = string.Equals(pdf.Status, "Ready", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(pdf.Status, "Completed", StringComparison.OrdinalIgnoreCase);
        var emoji = isSuccess ? "\u2705" : "\u274c";
        var statusText = isSuccess ? "Documento pronto" : "Elaborazione fallita";
        var headerText = $"{emoji} {statusText}";

        var blocks = new List<object>
        {
            new
            {
                type = "header",
                text = new { type = "plain_text", text = headerText, emoji = true }
            },
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text = $"\ud83d\udcc4 *File*: {pdf.FileName}" }
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
