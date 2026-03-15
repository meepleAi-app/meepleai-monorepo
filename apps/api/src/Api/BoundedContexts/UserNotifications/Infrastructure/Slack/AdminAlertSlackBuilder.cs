using System.Globalization;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Slack;

/// <summary>
/// Block Kit builder for admin/team channel alert notifications.
/// Uses severity-based emoji formatting consistent with SlackAlertChannel patterns.
/// Handles all admin notification types (circuit breaker, rate limit, model deprecation, etc.).
/// </summary>
internal sealed class AdminAlertSlackBuilder : ISlackMessageBuilder
{
    public bool CanHandle(NotificationType type)
    {
        // All admin notification types
        return type == NotificationType.AdminNewShareRequest
            || type == NotificationType.AdminStaleShareRequests
            || type == NotificationType.AdminReviewLockExpiring
            || type == NotificationType.AdminSharedGameSubmitted
            || type == NotificationType.AdminOpenRouterRpmAlert
            || type == NotificationType.AdminOpenRouterBudgetAlert
            || type == NotificationType.AdminCircuitBreakerStateChanged
            || type == NotificationType.AdminOpenRouterDailySummary
            || type == NotificationType.AdminRedisRateLimitingDegraded
            || type == NotificationType.AdminModelDeprecated
            || type == NotificationType.AdminModelAutoFallback;
    }

    public object BuildMessage(INotificationPayload payload, string? deepLinkPath)
    {
        if (payload is not GenericPayload alert)
        {
            throw new ArgumentException($"Expected {nameof(GenericPayload)} but received {payload.GetType().Name}", nameof(payload));
        }

        var severity = ClassifySeverity(alert.Title);
        var emoji = GetSeverityEmoji(severity);
        var color = GetSeverityColor(severity);

        var blocks = new List<object>
        {
            new
            {
                type = "header",
                text = new { type = "plain_text", text = $"{emoji} [{severity}] {alert.Title}", emoji = true }
            },
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text = alert.Body }
            },
            new
            {
                type = "context",
                elements = new object[]
                {
                    new { type = "mrkdwn", text = $"MeepleAI Monitoring | {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC" }
                }
            }
        };

        // Use attachments for color sidebar (Block Kit does not support block-level colors,
        // so we combine blocks + a color attachment for visual severity indication)
        return new
        {
            attachments = new object[]
            {
                new { color, blocks }
            }
        };
    }

    /// <summary>
    /// Classifies severity from the alert title, matching SlackAlertChannel patterns.
    /// Defaults to INFO for unrecognized titles.
    /// </summary>
    internal static string ClassifySeverity(string title)
    {
        var upper = title.ToUpper(CultureInfo.InvariantCulture);

        if (upper.Contains("CRITICAL", StringComparison.Ordinal)
            || upper.Contains("CIRCUIT BREAKER", StringComparison.Ordinal)
            || upper.Contains("DEGRADED", StringComparison.Ordinal))
        {
            return "CRITICAL";
        }

        if (upper.Contains("WARNING", StringComparison.Ordinal)
            || upper.Contains("STALE", StringComparison.Ordinal)
            || upper.Contains("EXPIRING", StringComparison.Ordinal)
            || upper.Contains("DEPRECATED", StringComparison.Ordinal)
            || upper.Contains("BUDGET", StringComparison.Ordinal)
            || upper.Contains("RPM", StringComparison.Ordinal))
        {
            return "WARNING";
        }

        return "INFO";
    }

    private static string GetSeverityEmoji(string severity) => severity switch
    {
        "CRITICAL" => "\ud83d\udea8",
        "WARNING" => "\u26a0\ufe0f",
        _ => "\u2139\ufe0f"
    };

    /// <summary>
    /// Returns Slack attachment color matching the SlackAlertChannel convention:
    /// CRITICAL = danger, WARNING = warning, INFO = informational blue.
    /// </summary>
    private static string GetSeverityColor(string severity) => severity switch
    {
        "CRITICAL" => "danger",
        "WARNING" => "warning",
        _ => "#1967d2"
    };
}
