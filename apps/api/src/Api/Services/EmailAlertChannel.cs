using System.Globalization;
using Api.Helpers;
using Api.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Api.Services;

/// <summary>
/// Email alert channel for health/monitoring alerts.
/// OPS-07: Email notifications for alerts.
/// Delivery is delegated to <see cref="IEmailService"/> — the shared, authenticated
/// SMTP_* transport used by all other outbound mail — instead of a self-configured
/// SmtpClient. The former channel-local SmtpClient was built from Alerting:Email:*
/// (Username/Password/From), which is empty on staging, so alert mail went out
/// unauthenticated and Gmail rejected it with "5.7.0 Authentication Required".
/// </summary>
internal class EmailAlertChannel : IAlertChannel
{
    private readonly EmailConfiguration _config;
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailAlertChannel> _logger;

    public string ChannelName => "Email";

    public EmailAlertChannel(
        IOptions<AlertingConfiguration> config,
        IEmailService emailService,
        ILogger<EmailAlertChannel> logger)
    {
        _config = config.Value.Email;
        _emailService = emailService;
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
            _logger.LogDebug("Email channel is disabled");
            return false;
        }

        if (_config.To.Count == 0)
        {
            _logger.LogWarning("Email channel is not properly configured (no recipients)");
            return false;
        }

        var subject = severity.ToUpper(CultureInfo.InvariantCulture) switch
        {
            "CRITICAL" => $"🚨 [CRITICAL] {alertType} - MeepleAI",
            "WARNING" => $"⚠️ [WARNING] {alertType} - MeepleAI",
            _ => $"ℹ️ [INFO] {alertType} - MeepleAI"
        };

        var body = FormatEmailBody(alertType, severity, message, metadata);

        try
        {
            // Transport (SMTP auth, From address, TLS) is owned by EmailService, which uses
            // the authenticated SMTP_* credentials. All-or-nothing: a throw on any recipient
            // stops the loop and yields false, mirroring the original single-try semantics.
            foreach (var recipient in _config.To)
            {
                await _emailService
                    .SendRawEmailAsync(recipient, subject, body, cancellationToken)
                    .ConfigureAwait(false);
            }

            _logger.LogInformation(
                "Email alert sent to {Recipients} for {AlertType}",
                string.Join(", ", _config.To.Select(DataMasking.MaskEmail)),
                LogSanitizer.Sanitize(alertType));

            return true;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // RESILIENCE PATTERN: Email alert channel failures must return false, not throw
            // Rationale: Alert channels implement IAlertChannel which requires returning success/
            // failure status. Throwing would prevent other channels from executing (Slack, PagerDuty).
            // Caller (AlertingService) tracks per-channel results for graceful degradation.
            // Context: Email failures are typically external (SMTP down, authentication expired);
            // EmailService.SendRawEmailAsync surfaces them as InvalidOperationException.
            _logger.LogError(ex, "Failed to send email alert for {AlertType}", LogSanitizer.Sanitize(alertType));
            return false;
        }
    }

    private static string FormatEmailBody(
        string alertType,
        string severity,
        string message,
        IDictionary<string, object>? metadata)
    {
        var severityColor = severity.ToUpper(CultureInfo.InvariantCulture) switch
        {
            "CRITICAL" => "#d93025",
            "WARNING" => "#ea8600",
            _ => "#1967d2"
        };

        var severityEmoji = severity.ToUpper(CultureInfo.InvariantCulture) switch
        {
            "CRITICAL" => "🚨",
            "WARNING" => "⚠️",
            _ => "ℹ️"
        };

        var metadataHtml = "";
        if (metadata != null && metadata.Count > 0)
        {
            metadataHtml = "<h3>Metadata</h3><ul>";
            foreach (var (key, value) in metadata)
            {
                metadataHtml += $"<li><strong>{key}</strong>: {value}</li>";
            }
            metadataHtml += "</ul>";
        }

        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; }}
        .alert {{ border-left: 4px solid {severityColor}; padding-left: 12px; margin: 16px 0; }}
        .alert h2 {{ color: {severityColor}; }}
    </style>
</head>
<body>
    <div class='alert'>
        <h2>{severityEmoji} {severity.ToUpper(CultureInfo.InvariantCulture)} ALERT</h2>
        <h3>{alertType}</h3>
        <p><strong>Message:</strong> {message}</p>
        <p><strong>Triggered:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
        {metadataHtml}
    </div>
    <hr />
    <p><small>This is an automated alert from MeepleAI Monitoring System.</small></p>
</body>
</html>";
    }
}
