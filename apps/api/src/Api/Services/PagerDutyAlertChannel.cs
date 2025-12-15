using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Api.Services;

/// <summary>
/// PagerDuty alert channel using Events API v2.
/// OPS-07: PagerDuty incident creation for critical alerts.
/// </summary>
internal class PagerDutyAlertChannel : IAlertChannel
{
    private readonly PagerDutyConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PagerDutyAlertChannel> _logger;

#pragma warning disable S1075 // URIs should not be hardcoded
    // Justification: Official PagerDuty Events API endpoint (public contract, never changes)
    // https://developer.pagerduty.com/api-reference/368ae3d8c8b4e-send-an-event-to-pager-duty
    private const string PagerDutyEventsApiUrl = "https://events.pagerduty.com/v2/enqueue";
#pragma warning restore S1075

    public string ChannelName => "PagerDuty";

    public PagerDutyAlertChannel(
        IOptions<AlertingConfiguration> config,
        IHttpClientFactory httpClientFactory,
        ILogger<PagerDutyAlertChannel> logger)
    {
        _config = config.Value.PagerDuty;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<bool> SendAsync(
        string alertType,
        string severity,
        string message,
        IDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            _logger.LogDebug("PagerDuty channel is disabled");
            return false;
        }

        if (string.IsNullOrEmpty(_config.IntegrationKey))
        {
            _logger.LogWarning("PagerDuty integration key is not configured");
            return false;
        }

        // Only send critical alerts to PagerDuty to avoid alert fatigue
        if (!string.Equals(severity.ToUpper(CultureInfo.InvariantCulture), "CRITICAL", StringComparison.Ordinal))
        {
            _logger.LogDebug(
                "Skipping PagerDuty for {Severity} alert (only CRITICAL alerts trigger incidents)",
                severity);
            return false;
        }

        try
        {
            var payload = BuildPagerDutyPayload(alertType, severity, message, metadata);
            // CA2000 suppression: HttpClient from IHttpClientFactory MUST NOT be disposed manually.
            // The factory manages HttpMessageHandler pooling and lifetime. See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests
#pragma warning disable CA2000 // Dispose objects before losing scope - False positive: IHttpClientFactory manages HttpClient lifetime
            var httpClient = _httpClientFactory.CreateClient();
#pragma warning restore CA2000 // Dispose objects before losing scope

            var response = await httpClient.PostAsJsonAsync(
                PagerDutyEventsApiUrl,
                payload,
                cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "PagerDuty incident created for {AlertType}",
                    alertType);
                return true;
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning(
                "PagerDuty API returned {StatusCode}: {Error}",
                response.StatusCode,
                errorContent);
            return false;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // RESILIENCE PATTERN: PagerDuty alert channel failures must return false, not throw
            // Rationale: Alert channels implement IAlertChannel which requires returning success/
            // failure status. Throwing would prevent other channels from executing (email, Slack).
            // Caller (AlertingService) tracks per-channel results for graceful degradation.
            // Context: PagerDuty failures are typically external (API timeout, rate limit, auth)
            _logger.LogError(ex, "Failed to send PagerDuty alert for {AlertType}", alertType);
            return false;
        }
    }

    private object BuildPagerDutyPayload(
        string alertType,
        string severity,
        string message,
        IDictionary<string, object>? metadata)
    {
        var customDetails = new Dictionary<string, object>
(StringComparer.Ordinal)
        {
            ["alert_type"] = alertType,
            ["severity"] = severity,
            ["message"] = message,
            ["triggered_at"] = DateTime.UtcNow.ToString("O")
        };

        if (metadata != null)
        {
            foreach (var (key, value) in metadata)
            {
                customDetails[key] = value;
            }
        }

        return new
        {
            routing_key = _config.IntegrationKey,
            event_action = "trigger",
            dedup_key = $"meepleai-{alertType}", // Group similar alerts
            payload = new
            {
                summary = $"[{severity.ToUpper(CultureInfo.InvariantCulture)}] {alertType}: {message}",
                severity = severity.ToLower(CultureInfo.InvariantCulture), // PagerDuty uses lowercase
                source = "meepleai-api",
                timestamp = DateTime.UtcNow.ToString("O"),
                custom_details = customDetails
            }
        };
    }
}
