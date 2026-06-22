using System.Net;
using System.Net.Mail;
using Api.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.Services.Email;

/// <summary>
/// Legacy SMTP <see cref="IEmailSender"/> implementation using <see cref="SmtpClient"/>.
/// Retained for development / fallback when Resend is not configured.
/// </summary>
/// <remarks>
/// Issue #1629: extracted from the original <c>EmailService</c> so the transport choice
/// (SMTP vs Resend) is a DI concern, not hard-coded inside template-building methods.
/// </remarks>
internal sealed class SmtpEmailSender : IEmailSender
{
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string? _smtpUsername;
    private readonly string? _smtpPassword;
    private readonly bool _enableSsl;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _smtpHost = configuration["Email:SmtpHost"] ?? configuration["SMTP_HOST"] ?? "localhost";
        _smtpPort = int.Parse(
            configuration["Email:SmtpPort"] ?? configuration["SMTP_PORT"] ?? "587",
            System.Globalization.CultureInfo.InvariantCulture);
        _smtpUsername = configuration["Email:SmtpUsername"] ?? configuration["SMTP_USER"];
        _smtpPassword = configuration["Email:SmtpPassword"]
            ?? configuration["SMTP_PASSWORD"]
            ?? configuration["GMAIL_APP_PASSWORD"];
        _enableSsl = bool.Parse(configuration["Email:EnableSsl"] ?? "true");
    }

    public string ProviderName => "smtp";

    public async Task SendAsync(EmailRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(request.FromEmail, request.FromName);
            message.To.Add(new MailAddress(request.ToEmail));
            message.Subject = request.Subject;
            message.Body = request.HtmlBody;
            message.IsBodyHtml = true;

            if (!string.IsNullOrWhiteSpace(request.ReplyTo))
            {
                message.ReplyToList.Add(new MailAddress(request.ReplyTo));
            }

            using var smtpClient = new SmtpClient(_smtpHost, _smtpPort);
            smtpClient.EnableSsl = _enableSsl;

            if (!string.IsNullOrEmpty(_smtpUsername) && !string.IsNullOrEmpty(_smtpPassword))
            {
                smtpClient.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
            }

            await smtpClient.SendMailAsync(message, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "SMTP accepted email to {Email}",
                DataMasking.MaskEmail(request.ToEmail));
        }
#pragma warning disable CA1031 // wrap any transport error into InvalidOperationException
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            _logger.LogError(
                ex,
                "SMTP error sending email to {Email}",
                DataMasking.MaskEmail(request.ToEmail));
            throw new InvalidOperationException(
                $"Failed to send email via SMTP to {DataMasking.MaskEmail(request.ToEmail)}.", ex);
        }
    }
}
