using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Slack;

/// <summary>
/// Block Kit builder for PDF processing notifications (ready or failed).
/// Produces a status header with emoji and a link to the document in MeepleAI.
/// </summary>
internal sealed class PdfProcessingSlackBuilder : ISlackMessageBuilder
{
    private readonly string _frontendBaseUrl;

    public PdfProcessingSlackBuilder(IConfiguration configuration)
    {
#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback value
        _frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? "https://meepleai.app";
#pragma warning restore S1075
    }

    public bool CanHandle(NotificationType type)
    {
        return type == NotificationType.PdfUploadCompleted
            || type == NotificationType.ProcessingFailed
            || type == NotificationType.ProcessingJobCompleted
            || type == NotificationType.ProcessingJobFailed;
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
