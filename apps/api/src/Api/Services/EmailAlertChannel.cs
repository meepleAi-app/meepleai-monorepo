using System.Globalization;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Api.Services;

/// <summary>
/// Email alert channel using SMTP.
/// OPS-07: Email notifications for alerts.
/// </summary>
public class EmailAlertChannel : IAlertChannel
{
    private readonly EmailConfiguration _config;
    private readonly ILogger<EmailAlertChannel> _logger;

    public string ChannelName => "Email";

    public EmailAlertChannel(
        IOptions<AlertingConfiguration> config,
        ILogger<EmailAlertChannel> logger)
    {
        _config = config.Value.Email;
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
            _logger.LogDebug("Email channel is disabled");
            return false;
        }

        if (string.IsNullOrEmpty(_config.SmtpHost) || _config.To.Count == 0)
        {
            _logger.LogWarning("Email channel is not properly configured (missing SMTP host or recipients)");
            return false;
        }

        try
        {
            using var client = new SmtpClient(_config.SmtpHost, _config.SmtpPort)
            {
                EnableSsl = _config.UseTls,
                Credentials = !string.IsNullOrEmpty(_config.Username)
                    ? new NetworkCredential(_config.Username, _config.Password)
                    : null
            };

            var subject = severity.ToUpper(CultureInfo.InvariantCulture) switch
            {
                "CRITICAL" => $"🚨 [CRITICAL] {alertType} - MeepleAI",
                "WARNING" => $"⚠️ [WARNING] {alertType} - MeepleAI",
                _ => $"ℹ️ [INFO] {alertType} - MeepleAI"
            };

            var body = FormatEmailBody(alertType, severity, message, metadata);

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(_config.From),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            foreach (var recipient in _config.To)
            {
                mailMessage.To.Add(recipient);
            }

            await client.SendMailAsync(mailMessage, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Email alert sent to {Recipients} for {AlertType}",
                string.Join(", ", _config.To),
                alertType);

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
            // Context: Email failures are typically external (SMTP down, authentication expired)
            _logger.LogError(ex, "Failed to send email alert for {AlertType}", alertType);
            return false;
        }
    }

    private static string FormatEmailBody(
        string alertType,
        string severity,
        string message,
        Dictionary<string, object>? metadata)
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
