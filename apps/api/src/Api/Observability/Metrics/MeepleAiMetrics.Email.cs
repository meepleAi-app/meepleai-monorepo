// #4845: email transport counters
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    /// <summary>
    /// Incremented once for every email the active <c>IEmailSender</c> failed to hand over, whatever the mail.
    /// Tags:
    ///   provider — the transport that failed (<c>smtp</c>, <c>resend</c>).
    ///   kind     — which mail it was (<c>password_reset</c>, <c>email_verification</c>, ...), a short fixed
    ///              label chosen at the call site, so the cardinality stays bounded.
    /// A caller that swallows the failure on purpose (a registration must not fail because the verification mail
    /// did not leave) leaves only a log line; this is what lets someone see that mail is not leaving (#4845).
    /// A send the caller cancelled (its token) is not a failure and is not counted; a transport timeout is.
    /// It counts the mails routed through <c>EmailService.SendViaTransportAsync</c>: the account mails still
    /// on a hand-built <c>SmtpClient</c> are not in it until they move (#4845).
    /// </summary>
    public static readonly Counter<long> EmailSendFailures = Meter.CreateCounter<long>(
        name: "meepleai.email.send_failures.total",
        unit: "failures",
        description: "Emails the active transport failed to send, by provider and kind of mail (#4845)");
}
