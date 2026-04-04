namespace Api.BoundedContexts.UserNotifications.Infrastructure.Configuration;

/// <summary>
/// Configuration options for Slack notification integration.
/// Bound from appsettings section "SlackNotification".
/// </summary>
internal sealed class SlackNotificationConfiguration
{
    public const string SectionName = "SlackNotification";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SigningSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public Dictionary<string, SlackTeamChannelSettings> TeamChannels { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Settings for a Slack team channel that receives broadcast notifications.
/// Configured per-workspace with type filtering.
/// </summary>
internal sealed class SlackTeamChannelSettings
{
    public string WebhookUrl { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public List<string> Types { get; set; } = [];
}
