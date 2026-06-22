using System.Collections.Generic;

namespace Api.BoundedContexts.Administration.Infrastructure.External;

/// <summary>
/// Client abstraction for Slack incoming webhooks used by the alerting subsystem
/// (Issue #1840 — SP5 F4-C7 Alerts re-skin).
///
/// <para>This client is intentionally distinct from the legacy
/// <see cref="Api.Services.SlackAlertChannel"/> (OPS-07) which is statically
/// configured via <c>AlertingConfiguration:Slack</c>. The new client accepts
/// the webhook URL as an argument so the URL can come from the
/// <c>AlertChannel</c> aggregate (admin-configurable per channel) and so that
/// the test-connection endpoint can probe arbitrary URLs without mutating
/// global config.</para>
///
/// <para>Retry policy is applied at the <see cref="System.Net.Http.HttpClient"/>
/// pipeline level via Polly (registered in
/// <c>AdministrationServiceExtensions</c>) to mirror the pattern used by
/// <c>PrometheusHttpClient</c> / <c>OpenRouterService</c>.</para>
/// </summary>
internal interface ISlackWebhookClient
{
    /// <summary>
    /// Posts an alert message to the supplied Slack incoming webhook URL.
    /// </summary>
    /// <remarks>
    /// Implementations MUST translate transport errors (network failure, 5xx,
    /// timeout) into a <see cref="SlackSendResult"/> with <c>Success=false</c>
    /// rather than propagating exceptions — caller (ChannelDispatchHandler)
    /// must continue dispatching to other channels even if Slack is down.
    /// </remarks>
    Task<SlackSendResult> SendAsync(string webhookUrl, SlackMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Probes a webhook URL by posting a benign "connection test" message.
    /// Returns whether Slack accepted the request (HTTP 200) and a short
    /// human-readable diagnostic.
    /// </summary>
    Task<SlackTestResult> TestConnectionAsync(string webhookUrl, CancellationToken cancellationToken);
}

/// <summary>
/// Minimal Slack message payload. Mirrors the legacy
/// <c>BuildSlackPayload</c> shape from <see cref="Api.Services.SlackAlertChannel"/>
/// but exposes only the fields the alerting subsystem needs.
/// </summary>
internal sealed record SlackMessage(
    string Text,
    string? Channel = null,
    IReadOnlyList<SlackField>? Fields = null,
    string? Severity = null);

/// <summary>
/// Key/value attribute rendered as a Slack attachment field
/// (max 5 fields recommended by Slack UX).
/// </summary>
internal sealed record SlackField(string Title, string Value, bool Short = true);

/// <summary>
/// Result of <see cref="ISlackWebhookClient.SendAsync"/>.
/// </summary>
internal sealed record SlackSendResult(bool Success, string? ErrorMessage = null, int? StatusCode = null);

/// <summary>
/// Result of <see cref="ISlackWebhookClient.TestConnectionAsync"/>.
/// </summary>
internal sealed record SlackTestResult(bool Success, string Message, int? StatusCode = null);
