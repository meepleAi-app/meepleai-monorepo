using System.Globalization;
using System.Text.Json;
using Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels;
using Api.BoundedContexts.Administration.Domain.Events;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.External;
using Api.Helpers;
using Api.Infrastructure.Security;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.EventHandlers;

/// <summary>
/// Fans out an <see cref="AlertFiredEvent"/> to every channel listed on the
/// originating <c>AlertRule</c> (Issue #1840 SP5 F4-C7).
///
/// <para>Three behaviours guarantee safe operation under partial failure:
/// <list type="number">
///   <item>When <see cref="AlertFiredEvent.IsDryRun"/>=true, no transport call
///         is made — the event still flows through the
///         <c>DomainEventLogPersistenceHandler</c> + SSE broadcaster so the
///         AlertActivityFeed can render the "DRY RUN" badge.</item>
///   <item>Each channel is dispatched in isolation. A failure on Slack does
///         NOT cancel Email, and vice versa. We collect per-channel results
///         but never propagate exceptions to MediatR — that would prevent
///         other notification handlers (audit log) from running.</item>
///   <item>The channel transport client returns a structured success/error
///         object so we can record the diagnostic on the <c>AlertChannel</c>
///         aggregate in a follow-up (auto-update LastTestStatus on real
///         dispatch — deferred to keep #1840 scope minimal).</item>
/// </list>
/// </para>
/// </summary>
internal sealed class ChannelDispatchHandler : INotificationHandler<AlertFiredEvent>
{
    private readonly IAlertChannelRepository _channelRepository;
    private readonly ISlackWebhookClient _slackClient;
    private readonly IEmailService _emailService;
    private readonly ILogger<ChannelDispatchHandler> _logger;

    public ChannelDispatchHandler(
        IAlertChannelRepository channelRepository,
        ISlackWebhookClient slackClient,
        IEmailService emailService,
        ILogger<ChannelDispatchHandler> logger)
    {
        _channelRepository = channelRepository ?? throw new ArgumentNullException(nameof(channelRepository));
        _slackClient = slackClient ?? throw new ArgumentNullException(nameof(slackClient));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(AlertFiredEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification.IsDryRun)
        {
            _logger.LogInformation(
                "[ChannelDispatch] DRY RUN — rule={Rule} severity={Severity} channels=[{Channels}] · no transport call made",
                LogSanitizer.Sanitize(notification.RuleName),
                notification.Severity,
                string.Join(',', notification.Channels));
            return;
        }

        // Dispatch each channel in its own task so a slow Slack webhook doesn't
        // block the Email send. Each branch swallows exceptions internally.
        var dispatchTasks = notification.Channels
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(channel => DispatchSingleAsync(channel, notification, cancellationToken));

        await Task.WhenAll(dispatchTasks).ConfigureAwait(false);
    }

    private async Task DispatchSingleAsync(
        string channelName,
        AlertFiredEvent notification,
        CancellationToken cancellationToken)
    {
        if (!TryParseChannel(channelName, out var type))
        {
            _logger.LogWarning(
                "[ChannelDispatch] Unknown channel '{Channel}' for rule {Rule} — skipping",
                LogSanitizer.Sanitize(channelName),
                LogSanitizer.Sanitize(notification.RuleName));
            return;
        }

        var channel = await _channelRepository.GetByTypeAsync(type, cancellationToken).ConfigureAwait(false);
        if (channel is null || !channel.IsEnabled)
        {
            _logger.LogInformation(
                "[ChannelDispatch] Channel {Type} not configured or disabled — skipping rule {Rule}",
                type, LogSanitizer.Sanitize(notification.RuleName));
            return;
        }

        // Issue #1941 / iso-2 Fix 1: pre-flight idempotency check. If this channel already
        // dispatched the incoming AlertFiredEvent, skip to avoid double-pinging oncall
        // (Slack webhook + Email SMTP have no remote dedup). Dry-run and test fires are
        // exempt — those use IsDryRun/IsTest flags, not LastDispatchedEventId, and admins
        // legitimately may want to test the same channel multiple times.
        if (!notification.IsDryRun
            && !notification.IsTest
            && channel.LastDispatchedEventId == notification.EventId)
        {
            _logger.LogDebug(
                "[ChannelDispatch] Skipping {Type} dispatch for rule {Rule}: event {EventId} already dispatched (iso-2)",
                type, LogSanitizer.Sanitize(notification.RuleName), notification.EventId);
            return;
        }

        try
        {
            switch (type)
            {
                case AlertChannelType.Slack:
                    await DispatchSlackAsync(channel, notification, cancellationToken).ConfigureAwait(false);
                    break;
                case AlertChannelType.Email:
                    await DispatchEmailAsync(channel, notification, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    _logger.LogWarning(
                        "[ChannelDispatch] No dispatch implementation for channel type {Type}",
                        type);
                    break;
            }

            // Issue #1941 / iso-2 Fix 1: only mark dispatched on real (non-dry-run, non-test)
            // alerts that completed without throwing. Concurrency: parallel sibling channels
            // each upsert their own row, so RowVersion contention is bounded per-channel.
            if (!notification.IsDryRun && !notification.IsTest)
            {
                channel.MarkDispatched(notification.EventId);
                await _channelRepository.UpsertAsync(channel, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // Swallow per-channel failure — sibling channels must still run
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(
                ex,
                "[ChannelDispatch] Channel {Type} dispatch failed for rule {Rule}",
                type,
                LogSanitizer.Sanitize(notification.RuleName));
        }
    }

    private async Task DispatchSlackAsync(
        AlertChannel channel,
        AlertFiredEvent notification,
        CancellationToken cancellationToken)
    {
        if (!TryParseSlackConfig(channel.ConfigJson, out var slackConfig))
        {
            _logger.LogWarning(
                "[ChannelDispatch] Slack channel config is malformed — skipping rule {Rule}",
                LogSanitizer.Sanitize(notification.RuleName));
            return;
        }

        var message = new SlackMessage(
            Text: BuildSlackText(notification),
            Channel: slackConfig.Channel,
            Fields: BuildSlackFields(notification),
            Severity: notification.Severity.ToString());

        var result = await _slackClient
            .SendAsync(slackConfig.WebhookUrl, message, cancellationToken)
            .ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogInformation(
                "[ChannelDispatch] Slack dispatch OK for rule {Rule}",
                LogSanitizer.Sanitize(notification.RuleName));
        }
        else
        {
            _logger.LogWarning(
                "[ChannelDispatch] Slack dispatch FAILED ({Status}) for rule {Rule}: {Error}",
                result.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? "<no status>",
                LogSanitizer.Sanitize(notification.RuleName),
                LogSanitizer.Sanitize(result.ErrorMessage ?? "<no message>"));
        }
    }

    private async Task DispatchEmailAsync(
        AlertChannel channel,
        AlertFiredEvent notification,
        CancellationToken cancellationToken)
    {
        if (!TryParseEmailConfig(channel.ConfigJson, out var emailConfig)
            || emailConfig.Recipients.Count == 0)
        {
            _logger.LogWarning(
                "[ChannelDispatch] Email channel config has no recipients — skipping rule {Rule}",
                LogSanitizer.Sanitize(notification.RuleName));
            return;
        }

        var subject = $"[{notification.Severity}] {notification.RuleName} — MeepleAI alert";
        var body = BuildEmailHtml(notification);

        foreach (var recipient in emailConfig.Recipients)
        {
            try
            {
                await _emailService.SendRawEmailAsync(recipient, subject, body, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031 // Per-recipient failure must not abort sibling deliveries
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogWarning(
                    ex,
                    "[ChannelDispatch] Email to {Recipient} FAILED for rule {Rule}",
                    DataMasking.MaskEmail(recipient),
                    LogSanitizer.Sanitize(notification.RuleName));
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Channel name + JSON parsing helpers
    // ─────────────────────────────────────────────────────────────────

    private static bool TryParseChannel(string raw, out AlertChannelType type)
    {
        try
        {
            type = AlertChannelTypeExtensions.FromWireValue(raw);
            return true;
        }
        catch (ArgumentException)
        {
            type = default;
            return false;
        }
    }

    private static bool TryParseSlackConfig(string json, out SlackChannelConfig config)
    {
        config = new SlackChannelConfig(string.Empty, null);
        try
        {
            var parsed = JsonSerializer.Deserialize<SlackChannelConfig>(json, JsonOptions);
            if (string.IsNullOrWhiteSpace(parsed.WebhookUrl))
            {
                return false;
            }
            config = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseEmailConfig(string json, out EmailChannelConfig config)
    {
        config = new EmailChannelConfig(Array.Empty<string>());
        try
        {
            var parsed = JsonSerializer.Deserialize<EmailChannelConfig>(json, JsonOptions);
            config = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ─────────────────────────────────────────────────────────────────
    // Message rendering
    // ─────────────────────────────────────────────────────────────────

    private static string BuildSlackText(AlertFiredEvent notification)
    {
        var prefix = notification.IsTest ? "[TEST] " : string.Empty;
        return $"{prefix}{notification.RuleName} — {notification.Metric} = {notification.Value}{notification.ThresholdUnit} " +
               $"(threshold {notification.Threshold}{notification.ThresholdUnit})";
    }

    private static IReadOnlyList<SlackField> BuildSlackFields(AlertFiredEvent notification) => new[]
    {
        new SlackField("Severity", notification.Severity.ToString(), Short: true),
        new SlackField("Rule", notification.RuleName, Short: true),
        new SlackField("Metric", notification.Metric, Short: false),
        new SlackField(
            "Value vs Threshold",
            $"{notification.Value}{notification.ThresholdUnit} / {notification.Threshold}{notification.ThresholdUnit}",
            Short: true),
        new SlackField("Triggered By", notification.TriggeredBy, Short: true),
    };

    private static string BuildEmailHtml(AlertFiredEvent notification)
    {
        var prefix = notification.IsTest ? "[TEST] " : string.Empty;
        return $@"
<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif; line-height: 1.5;'>
  <h2>{prefix}{System.Net.WebUtility.HtmlEncode(notification.RuleName)}</h2>
  <p><strong>Severity:</strong> {notification.Severity}</p>
  <p><strong>Metric:</strong> {System.Net.WebUtility.HtmlEncode(notification.Metric)}</p>
  <p><strong>Value vs Threshold:</strong> {notification.Value}{System.Net.WebUtility.HtmlEncode(notification.ThresholdUnit)} /
  {notification.Threshold}{System.Net.WebUtility.HtmlEncode(notification.ThresholdUnit)}</p>
  <p><strong>Triggered by:</strong> {System.Net.WebUtility.HtmlEncode(notification.TriggeredBy)}</p>
  <p><strong>At:</strong> {notification.OccurredAt:yyyy-MM-dd HH:mm:ss} UTC</p>
  <hr/>
  <p style='color: #888; font-size: 11px;'>MeepleAI Monitoring · this is an automated alert.</p>
</body>
</html>";
    }

    // Internal DTOs for channel configuration JSON parsing.
    private readonly record struct SlackChannelConfig(string WebhookUrl, string? Channel);

    private readonly record struct EmailChannelConfig(IReadOnlyList<string> Recipients);
}
