using Api.Infrastructure.Security;
using Api.Observability;
using Api.Services.Email;
using Api.SharedKernel.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Services;

internal partial class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IEmailSender _sender;
    private readonly string _fromAddress;
    private readonly string _fromName;
    private readonly string? _resetUrlOverride;
    private readonly IPublicBaseUrlProvider _publicBaseUrl;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The year in the footer of every template that does not take it as an argument (#4775). It used to be a
    /// literal «2025» typed into each of them; it is the injected clock's year, so a test can fix it.
    /// </summary>
    private int FooterYear => _timeProvider.GetUtcNow().Year;

    /// <summary>
    /// Legacy ctor used by existing unit tests that build EmailService by hand without
    /// providing an <see cref="IEmailSender"/>. The transport is then the SMTP sender, configured from
    /// the same <c>Email:Smtp*</c> keys.
    /// </summary>
    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        : this(configuration, logger, sender: null)
    {
    }

    /// <summary>
    /// Preferred ctor (resolved by DI). The <paramref name="sender"/> dependency centralises
    /// the choice between SMTP and Resend (Issue #1629), so individual <c>SendXxxEmailAsync</c>
    /// methods do not need to know about the transport. Without one the SMTP sender is built here, so a
    /// service made by hand and one resolved by DI send through the same code (#4845).
    /// </summary>
    public EmailService(
        IConfiguration configuration,
        ILogger<EmailService> logger,
        IEmailSender? sender,
        TimeProvider? timeProvider = null,
        IPublicBaseUrlProvider? publicBaseUrl = null)
    {
        _logger = logger;
        _sender = sender ?? new SmtpEmailSender(configuration, NullLogger<SmtpEmailSender>.Instance);
        _timeProvider = timeProvider ?? TimeProvider.System;

        // Load email configuration with fallback to SMTP_* env var names from email.secret
        // email.secret uses SMTP_HOST/SMTP_USER/SMTP_PASSWORD but EmailService originally
        // expected Email:SmtpHost/Email:SmtpUsername/Email:SmtpPassword — bridge the gap
        _fromAddress = configuration["Email:FromAddress"]
            ?? configuration["RESEND_FROM_EMAIL"]
            ?? configuration["SMTP_FROM_EMAIL"]
            ?? "noreply@meepleai.app";
        _fromName = configuration["Email:FromName"] ?? "MeepleAI";
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
    /// Routes the send through the <see cref="IEmailSender"/> (Resend or SMTP, by <c>EMAIL_PROVIDER</c>).
    /// Throws on transport failure — callers decide whether to swallow the exception or propagate it
    /// (Issue #1629). A failure is counted under the provider and the <paramref name="kind"/> of mail, so one
    /// that a caller swallows on purpose still shows up (#4845). What decides is the caller's token, not the
    /// exception type: a send the caller cancelled is not a failure, while a transport that timed out throws a
    /// <see cref="TaskCanceledException"/> too and is one.
    /// <c>kind</c> is the short fixed label of the mail, the tag of the failure counter; <c>toName</c> is the
    /// display name for the <c>To:</c> line, when the mail has one; <c>additionalToEmails</c> and <c>attachments</c>
    /// are the other recipients of the same message and the files it carries, which only the reports have.
    /// </summary>
    private async Task SendViaTransportAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string kind,
        CancellationToken cancellationToken,
        string? toName = null,
        IReadOnlyList<string>? additionalToEmails = null,
        IReadOnlyList<EmailRequestAttachment>? attachments = null)
    {
        try
        {
            await _sender.SendAsync(new EmailRequest
            {
                FromEmail = _fromAddress,
                FromName = _fromName,
                ToEmail = toEmail,
                ToName = toName,
                AdditionalToEmails = additionalToEmails,
                Subject = subject,
                HtmlBody = htmlBody,
                Attachments = attachments
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            MeepleAiMetrics.EmailSendFailures.Add(
                1,
                new KeyValuePair<string, object?>("provider", _sender.ProviderName),
                new KeyValuePair<string, object?>("kind", kind));
            throw;
        }
    }

    // ISSUE-4417: Raw email sending for queue processor.
    // #3250: delegate to SendViaTransportAsync so this honors the configured IEmailSender
    // (Resend/SMTP via EMAIL_PROVIDER) instead of a hardcoded SmtpClient. Previously it
    // always sent over SMTP, so on a Resend-only prod, alert (EmailAlertChannel) and queued
    // emails failed silently while templated emails succeeded via Resend.
    public async Task SendRawEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        try
        {
            await SendViaTransportAsync(toEmail, subject, htmlBody, "raw", ct).ConfigureAwait(false);

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
