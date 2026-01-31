namespace Api.Services;

/// <summary>
/// Interface for alert notification channels (Email, Slack, PagerDuty).
/// Strategy pattern for multi-channel alerting.
/// </summary>
internal interface IAlertChannel
{
    /// <summary>
    /// Name of the channel (e.g., "Email", "Slack", "PagerDuty").
    /// </summary>
    string ChannelName { get; }

    /// <summary>
    /// Send an alert notification through this channel.
    /// </summary>
    Task<bool> SendAsync(
        string alertType,
        string severity,
        string message,
        IDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);
}
