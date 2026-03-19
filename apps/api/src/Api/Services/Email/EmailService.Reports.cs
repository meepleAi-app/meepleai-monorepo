using System.Globalization;
using System.Net;
using System.Net.Mail;
using Api.Infrastructure.Security;

namespace Api.Services;

internal partial class EmailService
{
    // ISSUE-918: Report email delivery

    public async Task SendReportEmailAsync(
        IReadOnlyList<string> recipients,
        string reportName,
        string reportDescription,
        byte[] reportContent,
        string fileName,
        long fileSizeBytes,
        CancellationToken ct = default)
    {
        if (recipients is null || recipients.Count == 0)
        {
            _logger.LogWarning("No recipients provided for report email: {ReportName}", reportName);
            return;
        }

        const long MaxAttachmentSize = 10 * 1024 * 1024; // 10 MB
        if (fileSizeBytes > MaxAttachmentSize)
        {
            _logger.LogWarning(
                "Report too large for email attachment: {FileName} ({Size} bytes). Limit: {Limit} bytes",
                fileName, fileSizeBytes, MaxAttachmentSize);
            throw new InvalidOperationException($"Report size ({fileSizeBytes} bytes) exceeds email attachment limit ({MaxAttachmentSize} bytes)");
        }

        try
        {
            var subject = $"[MeepleAI] Report Ready: {reportName}";
            var body = BuildReportEmailBody(reportName, reportDescription, fileName, fileSizeBytes);

            using var message = new MailMessage();
            message.From = new MailAddress(_fromAddress, _fromName);

            foreach (var recipient in recipients)
            {
                message.To.Add(new MailAddress(recipient));
            }

            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            var attachment = new System.Net.Mail.Attachment(
                new System.IO.MemoryStream(reportContent),
                fileName);
            message.Attachments.Add(attachment);

            using var smtpClient = new SmtpClient(_smtpHost, _smtpPort);
            smtpClient.EnableSsl = _enableSsl;

            if (!string.IsNullOrEmpty(_smtpUsername) && !string.IsNullOrEmpty(_smtpPassword))
            {
                smtpClient.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
            }

            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Report email sent successfully: {ReportName} to {RecipientCount} recipients",
                reportName, recipients.Count);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // ADAPTER PATTERN: Wraps external SMTP service exceptions (authentication, network, timeout) into domain exception
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send report email: {ReportName} to {RecipientCount} recipients",
                reportName, recipients.Count);
            throw new InvalidOperationException($"Failed to send report email for {reportName}", ex);
        }
#pragma warning restore CA1031
    }

    public async Task SendReportFailureEmailAsync(
        IReadOnlyList<string> recipients,
        string reportName,
        string errorMessage,
        CancellationToken ct = default)
    {
        if (recipients is null || recipients.Count == 0)
        {
            _logger.LogWarning("No recipients provided for report failure email: {ReportName}", reportName);
            return;
        }

        try
        {
            var subject = $"[MeepleAI] Report Generation Failed: {reportName}";
            var body = BuildReportFailureEmailBody(reportName, errorMessage);

            using var message = new MailMessage();
            message.From = new MailAddress(_fromAddress, _fromName);

            foreach (var recipient in recipients)
            {
                message.To.Add(new MailAddress(recipient));
            }

            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            using var smtpClient = new SmtpClient(_smtpHost, _smtpPort);
            smtpClient.EnableSsl = _enableSsl;

            if (!string.IsNullOrEmpty(_smtpUsername) && !string.IsNullOrEmpty(_smtpPassword))
            {
                smtpClient.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
            }

            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Report failure email sent successfully: {ReportName} to {RecipientCount} recipients",
                reportName, recipients.Count);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // BACKGROUND SERVICE: Failure notification email is best-effort, must not propagate exceptions
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send report failure email: {ReportName} to {RecipientCount} recipients",
                reportName, recipients.Count);
        }
#pragma warning restore CA1031
    }

    // ===== Template builders =====

    private static string BuildReportEmailBody(
        string reportName,
        string reportDescription,
        string fileName,
        long fileSizeBytes)
    {
        var fileSizeMB = fileSizeBytes / (1024.0 * 1024.0);
        var generatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Report Ready</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #d1ecf1; padding: 20px; border-radius: 5px; border: 2px solid #0c5460; margin-bottom: 20px;"">
        <h2 style=""color: #0c5460; margin-top: 0;"">📊 Report Ready</h2>
        <p style=""margin: 0; color: #0c5460; font-weight: bold;"">{reportName}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Your scheduled report has been generated successfully.</p>

        <p><strong>Description:</strong> {reportDescription}</p>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-radius: 3px;"">
            <p style=""margin: 5px 0;""><strong>File:</strong> {fileName}</p>
            <p style=""margin: 5px 0;""><strong>Size:</strong> {fileSizeMB:F2} MB</p>
            <p style=""margin: 5px 0;""><strong>Generated:</strong> {generatedAt} UTC</p>
        </div>

        <p>The report is attached to this email. Please review the contents at your convenience.</p>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            This is an automated report delivery from your scheduled reports.
        </p>
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>This is an automated message, please do not reply to this email.</p>
        <p>&copy; 2025 MeepleAI. All rights reserved.</p>
    </div>
</body>
</html>
";
    }

    private static string BuildReportFailureEmailBody(string reportName, string errorMessage)
    {
        var failedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Report Generation Failed</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #f8d7da; padding: 20px; border-radius: 5px; border: 2px solid #dc3545; margin-bottom: 20px;"">
        <h2 style=""color: #721c24; margin-top: 0;"">⚠️ Report Generation Failed</h2>
        <p style=""margin: 0; color: #721c24; font-weight: bold;"">{reportName}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>An error occurred while generating your scheduled report.</p>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-left: 4px solid #dc3545; border-radius: 3px;"">
            <p style=""margin: 5px 0;""><strong>Failed At:</strong> {failedAt} UTC</p>
            <p style=""margin: 5px 0;""><strong>Error:</strong></p>
            <p style=""margin: 10px 0; padding: 10px; background-color: #fff; border: 1px solid #e0e0e0; font-family: monospace; font-size: 12px; word-break: break-word;"">{errorMessage}</p>
        </div>

        <p>The report generation will be retried on the next scheduled run. If this problem persists, please contact the administrator.</p>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            This is an automated notification from your scheduled reports monitoring system.
        </p>
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>This is an automated message, please do not reply to this email.</p>
        <p>&copy; 2025 MeepleAI. All rights reserved.</p>
    </div>
</body>
</html>
";
    }
}
