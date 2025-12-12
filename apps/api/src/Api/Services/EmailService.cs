using System.Globalization;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace Api.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly string _fromAddress;
    private readonly string _fromName;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string? _smtpUsername;
    private readonly string? _smtpPassword;
    private readonly bool _enableSsl;
    private readonly string _resetUrlBase;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        // S1075: Default values extracted to const
        const string DefaultResetUrlBase = "http://localhost:3000/reset-password";

        _logger = logger;

        // Load email configuration (S1450: configuration used only locally)
        _fromAddress = configuration["Email:FromAddress"] ?? "noreply@meepleai.dev";
        _fromName = configuration["Email:FromName"] ?? "MeepleAI";
        _smtpHost = configuration["Email:SmtpHost"] ?? "localhost";
        _smtpPort = int.Parse(configuration["Email:SmtpPort"] ?? "587", CultureInfo.InvariantCulture);
        _smtpUsername = configuration["Email:SmtpUsername"];
        _smtpPassword = configuration["Email:SmtpPassword"];
        _enableSsl = bool.Parse(configuration["Email:EnableSsl"] ?? "true");
        _resetUrlBase = configuration["Email:ResetUrlBase"] ?? DefaultResetUrlBase;
    }

    public async Task SendPasswordResetEmailAsync(
        string toEmail,
        string toName,
        string resetToken,
        CancellationToken ct = default)
    {
        try
        {
            var resetUrl = $"{_resetUrlBase}?token={Uri.EscapeDataString(resetToken)}";
            var subject = "Reset Your MeepleAI Password";
            var body = BuildPasswordResetEmailBody(toName, resetUrl);

            using var message = new MailMessage();
            message.From = new MailAddress(_fromAddress, _fromName);
            message.To.Add(new MailAddress(toEmail, toName));
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
                "Password reset email sent successfully to {Email}",
                toEmail);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Infrastructure adapter - Wraps external SMTP service exceptions (authentication, network, timeout) into domain exception
        // External service integration requires catching all SMTP exceptions to provide consistent error handling
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send password reset email to {Email}",
                toEmail);
            throw new InvalidOperationException("Failed to send password reset email", ex);
        }
#pragma warning restore CA1031
    }

    public async Task SendTwoFactorDisabledEmailAsync(
        string toEmail,
        string toName,
        bool wasAdminOverride,
        CancellationToken ct = default)
    {
        try
        {
            var subject = "Two-Factor Authentication Disabled";
            var body = BuildTwoFactorDisabledEmailBody(toName, wasAdminOverride);

            using var message = new MailMessage();
            message.From = new MailAddress(_fromAddress, _fromName);
            message.To.Add(new MailAddress(toEmail, toName));
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
                "Two-factor authentication disabled email sent successfully to {Email} (Admin override: {AdminOverride})",
                toEmail,
                wasAdminOverride);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Infrastructure adapter - Wraps external SMTP service exceptions (authentication, network, timeout) into domain exception
        // External service integration requires catching all SMTP exceptions to provide consistent error handling
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send two-factor authentication disabled email to {Email}",
                toEmail);
            throw new InvalidOperationException("Failed to send two-factor authentication disabled email", ex);
        }
#pragma warning restore CA1031
    }

    private static string BuildPasswordResetEmailBody(string userName, string resetUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Reset Your Password</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <h2 style=""color: #2c3e50; margin-top: 0;"">Password Reset Request</h2>

        <p>Hello {userName},</p>

        <p>We received a request to reset your password for your MeepleAI account. If you didn't make this request, you can safely ignore this email.</p>

        <p>To reset your password, click the button below:</p>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{resetUrl}"" style=""background-color: #3498db; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">Reset Password</a>
        </div>

        <p>Or copy and paste this link into your browser:</p>
        <p style=""word-break: break-all; color: #3498db;"">{resetUrl}</p>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            This password reset link will expire in 30 minutes for security reasons.
        </p>

        <p style=""font-size: 14px; color: #666;"">
            If you continue to have problems, please contact our support team.
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

    private static string BuildTwoFactorDisabledEmailBody(string userName, bool wasAdminOverride)
    {
        var reason = wasAdminOverride
            ? "An administrator has disabled two-factor authentication on your account, likely due to a support request for lost authenticator access."
            : "Two-factor authentication has been disabled on your account.";

        var warning = wasAdminOverride
            ? "If you did not request this change, please contact our support team immediately and consider re-enabling two-factor authentication."
            : "If you did not make this change, please contact our support team immediately and secure your account.";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Two-Factor Authentication Disabled</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #fff3cd; padding: 20px; border-radius: 5px; border: 2px solid #ffc107; margin-bottom: 20px;"">
        <h2 style=""color: #856404; margin-top: 0;"">Security Alert</h2>
        <p style=""margin: 0; color: #856404; font-weight: bold;"">Two-Factor Authentication Disabled</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hello {userName},</p>

        <p>{reason}</p>

        <p style=""margin-top: 20px; padding: 15px; background-color: #f8f9fa; border-left: 4px solid #ffc107; border-radius: 3px;"">
            <strong>Important:</strong> {warning}
        </p>

        <p style=""margin-top: 30px;"">To re-enable two-factor authentication:</p>
        <ol>
            <li>Log in to your MeepleAI account</li>
            <li>Go to Settings &gt; Privacy &gt; Two-Factor Authentication</li>
            <li>Follow the setup instructions</li>
        </ol>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            This notification was sent for security purposes to keep you informed of changes to your account.
        </p>

        <p style=""font-size: 14px; color: #666;"">
            If you have any questions or concerns, please contact our support team.
        </p>
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>This is an automated security notification, please do not reply to this email.</p>
        <p>&copy; 2025 MeepleAI. All rights reserved.</p>
    </div>
</body>
</html>
";
    }

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
        // Justification: Infrastructure adapter - Wraps external SMTP service exceptions (authentication, network, timeout) into domain exception
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
        // Justification: Background operation - Failure notification email is best-effort, must not propagate exceptions
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send report failure email: {ReportName} to {RecipientCount} recipients",
                reportName, recipients.Count);
        }
#pragma warning restore CA1031
    }

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
