using System.Globalization;
using System.Net;
using System.Net.Mail;
using Api.Infrastructure.Security;
using Microsoft.Extensions.Configuration;

namespace Api.Services;

internal partial class EmailService : IEmailService
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
    private readonly string _frontendBaseUrl;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        // S1075: Default values extracted to const
#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback value
        const string DefaultResetUrlBase = "http://localhost:3000/reset-password";
        const string DefaultFrontendBaseUrl = "http://localhost:3000";
#pragma warning restore S1075

        _logger = logger;

        // Load email configuration with fallback to SMTP_* env var names from email.secret
        // email.secret uses SMTP_HOST/SMTP_USER/SMTP_PASSWORD but EmailService originally
        // expected Email:SmtpHost/Email:SmtpUsername/Email:SmtpPassword — bridge the gap
        _fromAddress = configuration["Email:FromAddress"] ?? configuration["SMTP_FROM_EMAIL"] ?? "noreply@meepleai.dev";
        _fromName = configuration["Email:FromName"] ?? "MeepleAI";
        _smtpHost = configuration["Email:SmtpHost"] ?? configuration["SMTP_HOST"] ?? "localhost";
        _smtpPort = int.Parse(configuration["Email:SmtpPort"] ?? configuration["SMTP_PORT"] ?? "587", CultureInfo.InvariantCulture);
        _smtpUsername = configuration["Email:SmtpUsername"] ?? configuration["SMTP_USER"];
        _smtpPassword = configuration["Email:SmtpPassword"] ?? configuration["SMTP_PASSWORD"] ?? configuration["GMAIL_APP_PASSWORD"];
        _enableSsl = bool.Parse(configuration["Email:EnableSsl"] ?? "true");
        _resetUrlBase = configuration["Email:ResetUrlBase"] ?? DefaultResetUrlBase;
        _frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? DefaultFrontendBaseUrl;
    }

    // ISSUE-4417: Raw email sending for queue processor
    public async Task SendRawEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(_fromAddress, _fromName);
            message.To.Add(new MailAddress(toEmail));
            message.Subject = subject;
            message.Body = htmlBody;
            message.IsBodyHtml = true;

            using var smtpClient = new SmtpClient(_smtpHost, _smtpPort);
            smtpClient.EnableSsl = _enableSsl;

            if (!string.IsNullOrEmpty(_smtpUsername) && !string.IsNullOrEmpty(_smtpPassword))
            {
                smtpClient.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
            }

            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Raw email sent successfully to {Email}",
                DataMasking.MaskEmail(toEmail));
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(
                ex,
                "Failed to send raw email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send raw email", ex);
        }
    }
}
