using System.Net;
using System.Net.Mail;
using Api.Infrastructure.Security;

namespace Api.Services;

internal partial class EmailService
{
    // ISSUE-4220: PDF notification emails

    public async Task SendPdfReadyEmailAsync(
        string toEmail,
        string userName,
        string fileName,
        Guid pdfDocumentId,
        CancellationToken ct = default)
    {
        try
        {
            var subject = "Your PDF is Ready - MeepleAI";
            var body = BuildPdfReadyEmailBody(userName, fileName, pdfDocumentId);

            using var message = new MailMessage();
            message.From = new MailAddress(_fromAddress, _fromName);
            message.To.Add(new MailAddress(toEmail, userName));
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
                "PDF ready email sent successfully to {Email}",
                DataMasking.MaskEmail(toEmail));
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send PDF ready email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send PDF ready email", ex);
        }
#pragma warning restore CA1031
    }

    public async Task SendPdfFailedEmailAsync(
        string toEmail,
        string userName,
        string fileName,
        string errorMessage,
        CancellationToken ct = default)
    {
        try
        {
            var subject = "PDF Processing Failed - MeepleAI";
            var body = BuildPdfFailedEmailBody(userName, fileName, errorMessage);

            using var message = new MailMessage();
            message.From = new MailAddress(_fromAddress, _fromName);
            message.To.Add(new MailAddress(toEmail, userName));
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
                "PDF failed email sent successfully to {Email}",
                DataMasking.MaskEmail(toEmail));
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send PDF failed email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send PDF failed email", ex);
        }
#pragma warning restore CA1031
    }

    public async Task SendPdfRetryEmailAsync(
        string toEmail,
        string userName,
        string fileName,
        int retryCount,
        CancellationToken ct = default)
    {
        try
        {
            var subject = "PDF Processing Retry - MeepleAI";
            var body = BuildPdfRetryEmailBody(userName, fileName, retryCount);

            using var message = new MailMessage();
            message.From = new MailAddress(_fromAddress, _fromName);
            message.To.Add(new MailAddress(toEmail, userName));
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
                "PDF retry email sent successfully to {Email}",
                DataMasking.MaskEmail(toEmail));
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send PDF retry email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send PDF retry email", ex);
        }
#pragma warning restore CA1031
    }

    // ===== Template builders =====

    private string BuildPdfReadyEmailBody(
        string userName,
        string fileName,
        Guid pdfDocumentId)
    {
        var documentUrl = $"{_frontendBaseUrl}/documents/{pdfDocumentId}";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>PDF Ready</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #d4edda; padding: 20px; border-radius: 5px; border: 2px solid #28a745; margin-bottom: 20px;"">
        <h2 style=""color: #155724; margin-top: 0;"">✅ PDF Ready</h2>
        <p style=""margin: 0; color: #155724; font-weight: bold;"">{fileName}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hello {userName},</p>

        <p>Great news! Your PDF document <strong>{fileName}</strong> has been successfully processed and is now ready for AI queries.</p>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{documentUrl}"" style=""background-color: #28a745; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">Start Chatting</a>
        </div>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-radius: 3px;"">
            <p style=""margin: 0;""><strong>💡 Quick Tips:</strong></p>
            <ul style=""margin: 10px 0 0 0; padding-left: 20px;"">
                <li>Ask questions about the content</li>
                <li>Request summaries of sections</li>
                <li>Get clarification on complex topics</li>
            </ul>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            Happy exploring!
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

    private string BuildPdfFailedEmailBody(
        string userName,
        string fileName,
        string errorMessage)
    {
        var supportUrl = $"{_frontendBaseUrl}/support";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>PDF Processing Failed</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #f8d7da; padding: 20px; border-radius: 5px; border: 2px solid #dc3545; margin-bottom: 20px;"">
        <h2 style=""color: #721c24; margin-top: 0;"">⚠️ PDF Processing Failed</h2>
        <p style=""margin: 0; color: #721c24; font-weight: bold;"">{fileName}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hello {userName},</p>

        <p>Unfortunately, we encountered an issue while processing your PDF document <strong>{fileName}</strong>.</p>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-left: 4px solid #dc3545; border-radius: 3px;"">
            <p style=""margin: 0;""><strong>Error Details:</strong></p>
            <p style=""margin: 5px 0 0 0; color: #666;"">{errorMessage}</p>
        </div>

        <div style=""margin: 20px 0; padding: 15px; background-color: #fff3cd; border-left: 4px solid #ffc107; border-radius: 3px;"">
            <p style=""margin: 0;""><strong>💡 Common Solutions:</strong></p>
            <ul style=""margin: 10px 0 0 0; padding-left: 20px;"">
                <li>Ensure PDF is not password-protected</li>
                <li>Check file size is under limit</li>
                <li>Verify PDF is not corrupted</li>
            </ul>
        </div>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{supportUrl}"" style=""background-color: #6c757d; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">Contact Support</a>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            If the problem persists, please reach out to our support team.
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

    private string BuildPdfRetryEmailBody(
        string userName,
        string fileName,
        int retryCount)
    {
        var libraryUrl = $"{_frontendBaseUrl}/library";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>PDF Processing Retry</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #d1ecf1; padding: 20px; border-radius: 5px; border: 2px solid #17a2b8; margin-bottom: 20px;"">
        <h2 style=""color: #0c5460; margin-top: 0;"">🔄 PDF Retry #{retryCount}</h2>
        <p style=""margin: 0; color: #0c5460; font-weight: bold;"">{fileName}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hello {userName},</p>

        <p>We're retrying the processing of your PDF document <strong>{fileName}</strong>. This is attempt #{retryCount}.</p>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-radius: 3px;"">
            <p style=""margin: 0;""><strong>What's Happening:</strong></p>
            <p style=""margin: 5px 0 0 0; color: #666;"">Our system automatically retries failed documents to ensure successful processing. You'll be notified once processing completes.</p>
        </div>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{libraryUrl}"" style=""background-color: #17a2b8; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">View My Library</a>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            Thank you for your patience!
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
