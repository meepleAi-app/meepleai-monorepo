using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Api.Services;

/// <summary>
/// Slack alert channel using Incoming Webhooks.
/// OPS-07: Slack notifications for alerts.
/// </summary>
public class SlackAlertChannel : IAlertChannel
{
    private readonly SlackConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SlackAlertChannel> _logger;

    public string ChannelName => "Slack";

    public SlackAlertChannel(
        IOptions<AlertingConfiguration> config,
        IHttpClientFactory httpClientFactory,
        ILogger<SlackAlertChannel> logger)
    {
        _config = config.Value.Slack;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<bool> SendAsync(
        string alertType,
        string severity,
        string message,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            _logger.LogDebug("Slack channel is disabled");
            return false;
        }

        if (string.IsNullOrEmpty(_config.WebhookUrl))
        {
            _logger.LogWarning("Slack webhook URL is not configured");
            return false;
        }

        try
        {
            var payload = BuildSlackPayload(alertType, severity, message, metadata);
#pragma warning disable CA2000 // HttpClient lifetime managed by IHttpClientFactory
            var httpClient = _httpClientFactory.CreateClient();
#pragma warning restore CA2000

            var response = await httpClient.PostAsJsonAsync(
                _config.WebhookUrl,
                payload,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Slack alert sent to {Channel} for {AlertType}",
                    _config.Channel,
                    alertType);
                return true;
            }

            _logger.LogWarning(
                "Slack webhook returned {StatusCode} for {AlertType}",
                response.StatusCode,
                alertType);
            return false;
        }
        catch (Exception ex)
        {
            // RESILIENCE PATTERN: Slack alert channel failures must return false, not throw
            // Rationale: Alert channels implement IAlertChannel which requires returning success/
            // failure status. Throwing would prevent other channels from executing (email, PagerDuty).
            // Caller (AlertingService) tracks per-channel results for graceful degradation.
            // Context: Slack failures are typically external (webhook timeout, API rate limit)
            _logger.LogError(ex, "Failed to send Slack alert for {AlertType}", alertType);
            return false;
        }
    }

    private object BuildSlackPayload(
        string alertType,
        string severity,
        string message,
        Dictionary<string, object>? metadata)
    {
        var color = severity.ToUpper() switch
        {
            "CRITICAL" => "danger",
            "WARNING" => "warning",
            _ => "#1967d2"
        };

        var emoji = severity.ToUpper() switch
        {
            "CRITICAL" => ":rotating_light:",
            "WARNING" => ":warning:",
            _ => ":information_source:"
        };

        var fields = new List<object>
        {
            new { title = "Severity", value = severity.ToUpper(), @short = true },
            new { title = "Alert Type", value = alertType, @short = true },
            new { title = "Triggered", value = $"<!date^{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}^{{date_short_pretty}} {{time}}|{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC>", @short = true }
        };

        if (metadata != null)
        {
            foreach (var (key, value) in metadata.Take(5)) // Limit to 5 metadata fields
            {
                fields.Add(new { title = key, value = value.ToString(), @short = true });
            }
        }

        return new
        {
            channel = _config.Channel,
            username = "MeepleAI Alerts",
            icon_emoji = emoji,
            attachments = new[]
            {
                new
                {
                    color,
                    title = $"{emoji} {severity.ToUpper()}: {alertType}",
                    text = message,
                    fields,
                    footer = "MeepleAI Monitoring",
                    ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }
            }
        };
    }
}
