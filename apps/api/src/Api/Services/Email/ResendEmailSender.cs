using Api.Infrastructure.Security;
using Microsoft.Extensions.Logging;
using Resend;

namespace Api.Services.Email;

/// <summary>
/// <see cref="IEmailSender"/> implementation backed by the Resend transactional API.
/// </summary>
/// <remarks>
/// Issue #1629: introduced to replace Gmail SMTP for FROM @meepleai.app delivery.
/// Resend requires the sender domain to be verified (SPF + DKIM + DMARC) — see
/// docs/for-developers/operations/email-provider-setup.md.
/// </remarks>
internal sealed class ResendEmailSender : IEmailSender
{
    private readonly IResend _resend;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(IResend resend, ILogger<ResendEmailSender> logger)
    {
        _resend = resend ?? throw new ArgumentNullException(nameof(resend));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string ProviderName => "resend";

    public async Task SendAsync(EmailRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var message = new EmailMessage
        {
            From = $"{request.FromName} <{request.FromEmail}>",
            Subject = request.Subject,
            HtmlBody = request.HtmlBody
        };
        message.To.Add(request.ToEmail);

        if (!string.IsNullOrWhiteSpace(request.TextBody))
        {
            message.TextBody = request.TextBody;
        }

        if (!string.IsNullOrWhiteSpace(request.ReplyTo))
        {
            // Resend SDK exposes ReplyTo as nullable EmailAddressList — auto-initialized
            // by the EmailMessage constructor but typed as nullable in the SDK surface.
            message.ReplyTo!.Add(request.ReplyTo);
        }

        try
        {
            var response = await _resend.EmailSendAsync(message, cancellationToken).ConfigureAwait(false);

            if (response is null || !response.Success)
            {
                _logger.LogError(
                    "Resend EmailSendAsync returned non-success for {Email}",
                    DataMasking.MaskEmail(request.ToEmail));
                throw new InvalidOperationException(
                    $"Resend API rejected the email submission for {DataMasking.MaskEmail(request.ToEmail)}.");
            }

            _logger.LogInformation(
                "Resend accepted email {EmailId} to {Email}",
                response.Content,
                DataMasking.MaskEmail(request.ToEmail));
        }
        catch (ResendException ex)
        {
            _logger.LogError(
                ex,
                "Resend API error sending email to {Email}: {ErrorType}",
                DataMasking.MaskEmail(request.ToEmail),
                ex.ErrorType);
            throw new InvalidOperationException(
                $"Failed to send email via Resend: {ex.ErrorType}", ex);
        }
    }
}
