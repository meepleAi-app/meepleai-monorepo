using System.Globalization;
using System.Net;
using System.Net.Mail;
using Api.Infrastructure.Security;
using Api.Services.Email;
using Microsoft.Extensions.Configuration;

namespace Api.Services;

internal partial class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IEmailSender? _sender;
    private readonly string _fromAddress;
    private readonly string _fromName;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string? _smtpUsername;
    private readonly string? _smtpPassword;
    private readonly bool _enableSsl;
    private readonly string _resetUrlBase;
    private readonly string _frontendBaseUrl;

    /// <summary>
    /// Legacy ctor used by existing unit tests that mock EmailService directly without
    /// providing an <see cref="IEmailSender"/>. The transport falls back to in-method
    /// SmtpClient instantiation (original behaviour).
    /// </summary>
    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        : this(configuration, logger, sender: null)
    {
    }

    /// <summary>
    /// Preferred ctor (resolved by DI). The <paramref name="sender"/> dependency centralises
    /// the choice between SMTP and Resend (Issue #1629), so individual <c>SendXxxEmailAsync</c>
    /// methods do not need to know about the transport.
    /// </summary>
    public EmailService(
        IConfiguration configuration,
        ILogger<EmailService> logger,
        IEmailSender? sender)
    {
        // S1075: Default values extracted to const
#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback value
        const string DefaultResetUrlBase = "http://localhost:3000/reset-password";
        const string DefaultFrontendBaseUrl = "http://localhost:3000";
#pragma warning restore S1075

        _logger = logger;
        _sender = sender;

        // Load email configuration with fallback to SMTP_* env var names from email.secret
        // email.secret uses SMTP_HOST/SMTP_USER/SMTP_PASSWORD but EmailService originally
        // expected Email:SmtpHost/Email:SmtpUsername/Email:SmtpPassword — bridge the gap
        _fromAddress = configuration["Email:FromAddress"]
            ?? configuration["RESEND_FROM_EMAIL"]
            ?? configuration["SMTP_FROM_EMAIL"]
            ?? "noreply@meepleai.app";
        _fromName = configuration["Email:FromName"] ?? "MeepleAI";
        _smtpHost = configuration["Email:SmtpHost"] ?? configuration["SMTP_HOST"] ?? "localhost";
        _smtpPort = int.Parse(configuration["Email:SmtpPort"] ?? configuration["SMTP_PORT"] ?? "587", CultureInfo.InvariantCulture);
        _smtpUsername = configuration["Email:SmtpUsername"] ?? configuration["SMTP_USER"];
        _smtpPassword = configuration["Email:SmtpPassword"] ?? configuration["SMTP_PASSWORD"] ?? configuration["GMAIL_APP_PASSWORD"];
        _enableSsl = bool.Parse(configuration["Email:EnableSsl"] ?? "true");
        _resetUrlBase = configuration["Email:ResetUrlBase"] ?? DefaultResetUrlBase;
        _frontendBaseUrl = configuration["Frontend:BaseUrl"]
            ?? configuration["FRONTEND_BASE_URL"]
            ?? DefaultFrontendBaseUrl;
    }

    /// <summary>
    /// Routes the send through the registered <see cref="IEmailSender"/> when available
    /// (production/staging) or falls back to a direct <see cref="SmtpClient"/> path
    /// (legacy ctor / unit tests). Throws on transport failure — callers decide whether
    /// to swallow the exception or propagate it (Issue #1629).
    /// </summary>
    private async Task SendViaTransportAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        if (_sender is not null)
        {
            await _sender.SendAsync(new EmailRequest
            {
                FromEmail = _fromAddress,
                FromName = _fromName,
                ToEmail = toEmail,
                Subject = subject,
                HtmlBody = htmlBody
            }, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Legacy direct-SMTP path used only when no IEmailSender is injected
        // (kept for backward compatibility with existing unit tests).
        using var message = new MailMessage
        {
            From = new MailAddress(_fromAddress, _fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(toEmail));

        using var smtpClient = new SmtpClient(_smtpHost, _smtpPort)
        {
            EnableSsl = _enableSsl
        };

        if (!string.IsNullOrEmpty(_smtpUsername) && !string.IsNullOrEmpty(_smtpPassword))
        {
            smtpClient.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
        }

        await smtpClient.SendMailAsync(message, cancellationToken).ConfigureAwait(false);
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
