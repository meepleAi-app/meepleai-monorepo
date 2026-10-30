using System.Globalization;
using System.Net;
using System.Net.Mail;
using Api.Infrastructure.Security;
using Api.Services.Email;
using Api.SharedKernel.Services;
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
    private readonly string? _resetUrlOverride;
    private readonly IPublicBaseUrlProvider _publicBaseUrl;
    private readonly TimeProvider _timeProvider;

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
        IEmailSender? sender,
        TimeProvider? timeProvider = null,
        IPublicBaseUrlProvider? publicBaseUrl = null)
    {
        _logger = logger;
        _sender = sender;
        _timeProvider = timeProvider ?? TimeProvider.System;

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
        // An explicit full address for the reset page still wins; without one it hangs off the public
        // front-end URL (#4774: it used to fall back to localhost, and no deployment sets this key).
        _resetUrlOverride = string.IsNullOrWhiteSpace(configuration["Email:ResetUrlBase"])
            ? null
            : configuration["Email:ResetUrlBase"]!.Trim();

        // Legacy ctor (unit tests that build the service by hand): the same resolver over the same
        // configuration, outside a host, so without the local last resort.
        _publicBaseUrl = publicBaseUrl ?? new PublicBaseUrlProvider(configuration);
    }

    /// <summary>
    /// The public front-end URL, resolved when a link is composed and not when the service is built: an
    /// e-mail that has no link to carry must not fail because of one.
    /// </summary>
    private string FrontendBaseUrl => _publicBaseUrl.GetBaseUrl();

    /// <summary>The link in the password-reset e-mail.</summary>
    internal string BuildPasswordResetUrl(string resetToken)
    {
        var resetBase = _resetUrlOverride ?? $"{FrontendBaseUrl}/reset-password";
        return $"{resetBase}?token={Uri.EscapeDataString(resetToken)}";
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

    // ISSUE-4417: Raw email sending for queue processor.
    // #3250: delegate to SendViaTransportAsync so this honors the configured IEmailSender
    // (Resend/SMTP via EMAIL_PROVIDER) instead of a hardcoded SmtpClient. Previously it
    // always sent over SMTP, so on a Resend-only prod, alert (EmailAlertChannel) and queued
    // emails failed silently while templated emails succeeded via Resend. The direct-SMTP
    // fallback is preserved inside SendViaTransportAsync for the legacy ctor / unit tests.
    public async Task SendRawEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        try
        {
            await SendViaTransportAsync(toEmail, subject, htmlBody, ct).ConfigureAwait(false);

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
