using System.Net.Http.Json;
using Api.Helpers;

namespace Api.BoundedContexts.Administration.Infrastructure.External;

/// <summary>
/// Default <see cref="ISlackWebhookClient"/> implementation backed by
/// <see cref="HttpClient"/>. Used by the AlertChannel-aware dispatch path
/// for Issue #1840 (SP5 F4-C7 Alerts).
///
/// <para>The client is intentionally stateless — the webhook URL is passed
/// per call so a single registered <c>HttpClient</c> can service all alert
/// channels. Polly retry / circuit-breaker policies are attached at the
/// <c>HttpClient</c> pipeline level (see
/// <c>AdministrationServiceExtensions.AddAdministrationContext</c>).</para>
/// </summary>
internal sealed class SlackWebhookClient : ISlackWebhookClient
{
    /// <summary>
    /// Payload used by <see cref="ISlackWebhookClient.TestConnectionAsync"/>.
    /// Intentionally benign so probing a real channel doesn't spam alert noise.
    /// </summary>
    private const string TestMessageText = "MeepleAI alerts — connection test (no action required).";

    private readonly HttpClient _httpClient;
    private readonly ILogger<SlackWebhookClient> _logger;

    public SlackWebhookClient(HttpClient httpClient, ILogger<SlackWebhookClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SlackSendResult> SendAsync(string webhookUrl, SlackMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return new SlackSendResult(false, "Webhook URL is required.");
        }

        if (!Uri.TryCreate(webhookUrl, UriKind.Absolute, out var uri))
        {
            return new SlackSendResult(false, "Webhook URL is not a valid absolute URI.");
        }

        var payload = BuildPayload(message);

        try
        {
            var response = await _httpClient
                .PostAsJsonAsync(uri, payload, cancellationToken)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Slack webhook accepted message (status {Status})",
                    (int)response.StatusCode);
                return new SlackSendResult(true, null, (int)response.StatusCode);
            }

            var body = await SafeReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
            _logger.LogWarning(
                "Slack webhook returned {Status} — {Body}",
                (int)response.StatusCode,
                LogSanitizer.Sanitize(body));
            return new SlackSendResult(
                false,
                $"HTTP {(int)response.StatusCode} {response.ReasonPhrase ?? "Error"}".Trim(),
                (int)response.StatusCode);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types — alerting must never propagate to caller
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // Mirror the resilience contract used by SlackAlertChannel/EmailAlertChannel:
            // alert channels must not throw, because that would prevent other channels
            // (email, etc.) from executing in the same dispatch.
            _logger.LogError(ex, "Failed to post Slack webhook");
            return new SlackSendResult(false, ex.Message);
        }
    }

    public async Task<SlackTestResult> TestConnectionAsync(string webhookUrl, CancellationToken cancellationToken)
    {
        var probe = new SlackMessage(TestMessageText, Channel: null, Fields: null, Severity: "info");
        var result = await SendAsync(webhookUrl, probe, cancellationToken).ConfigureAwait(false);
        return new SlackTestResult(
            result.Success,
            result.Success ? "Connection OK" : result.ErrorMessage ?? "Unknown error",
            result.StatusCode);
    }

    private static object BuildPayload(SlackMessage message)
    {
        var severity = string.IsNullOrWhiteSpace(message.Severity) ? "info" : message.Severity;
        var color = severity.ToUpperInvariant() switch
        {
            "CRITICAL" or "ERROR" => "danger",
            "WARNING" => "warning",
            _ => "#1967d2",
        };
        var emoji = severity.ToUpperInvariant() switch
        {
            "CRITICAL" or "ERROR" => ":rotating_light:",
            "WARNING" => ":warning:",
            _ => ":information_source:",
        };

        var fields = (message.Fields ?? Array.Empty<SlackField>())
            .Take(5)
            .Select(f => new
            {
                title = f.Title,
                value = f.Value,
                @short = f.Short,
            })
            .ToList<object>();

        return new
        {
            channel = message.Channel,
            username = "MeepleAI Alerts",
            icon_emoji = emoji,
            attachments = new[]
            {
                new
                {
                    color,
                    text = message.Text,
                    fields,
                    footer = "MeepleAI Monitoring",
                    ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                },
            },
        };
    }

    private static async Task<string> SafeReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return body.Length > 256 ? body[..256] + "…" : body;
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (HttpRequestException)
        {
            return string.Empty;
        }
    }

}
