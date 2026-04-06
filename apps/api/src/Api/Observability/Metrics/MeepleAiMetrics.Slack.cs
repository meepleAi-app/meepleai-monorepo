// OPS-02: Slack delivery and queue metrics
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    #region Slack Metrics

    /// <summary>
    /// Counter for Slack messages successfully delivered.
    /// Labels: channel_type (slack_user|slack_team), notification_type.
    /// </summary>
    public static readonly Counter<long> SlackMessagesSentTotal = Meter.CreateCounter<long>(
        name: "meepleai.slack.messages.sent.total",
        unit: "messages",
        description: "Total Slack messages successfully delivered");

    /// <summary>
    /// Counter for Slack message delivery failures.
    /// Labels: channel_type, notification_type, error_type (rate_limit|token_revoked|http_error|unknown).
    /// </summary>
    public static readonly Counter<long> SlackMessagesFailedTotal = Meter.CreateCounter<long>(
        name: "meepleai.slack.messages.failed.total",
        unit: "messages",
        description: "Total Slack message delivery failures");

    /// <summary>
    /// Counter for Slack rate limit events (429 responses).
    /// Labels: team_id.
    /// </summary>
    public static readonly Counter<long> SlackRateLimitedTotal = Meter.CreateCounter<long>(
        name: "meepleai.slack.rate_limited.total",
        unit: "events",
        description: "Total Slack 429 rate limit events");

    /// <summary>
    /// Counter for Slack OAuth token revocation events.
    /// Incremented when a token is deactivated due to invalid_auth/token_revoked/account_inactive.
    /// </summary>
    public static readonly Counter<long> SlackTokenRevocationsTotal = Meter.CreateCounter<long>(
        name: "meepleai.slack.token_revocations.total",
        unit: "events",
        description: "Total Slack OAuth token revocation events");

    /// <summary>Records a successful Slack message delivery.</summary>
    public static void RecordSlackMessageSent(string channelType, string notificationType)
    {
        SlackMessagesSentTotal.Add(1, new TagList
        {
            { "channel_type", channelType },
            { "notification_type", notificationType }
        });
    }

    /// <summary>Records a Slack message delivery failure.</summary>
    public static void RecordSlackMessageFailed(string channelType, string notificationType, string errorType)
    {
        SlackMessagesFailedTotal.Add(1, new TagList
        {
            { "channel_type", channelType },
            { "notification_type", notificationType },
            { "error_type", errorType }
        });
    }

    /// <summary>Records a Slack 429 rate limit event for a team.</summary>
    public static void RecordSlackRateLimited(string teamId)
    {
        SlackRateLimitedTotal.Add(1, new TagList { { "team_id", teamId } });
    }

    /// <summary>Records a Slack OAuth token revocation (token deactivated).</summary>
    public static void RecordSlackTokenRevocation()
    {
        SlackTokenRevocationsTotal.Add(1);
    }

    #endregion
}
