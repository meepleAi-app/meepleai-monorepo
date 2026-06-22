namespace Api.BoundedContexts.UserNotifications.Application.Services;

/// <summary>
/// I5 (auth security fixes): caller-facing surface to enqueue an email
/// for asynchronous delivery via the outbox pattern.
///
/// Why an outbox instead of a direct synchronous send?
///   - Decouples domain transactions from email-provider availability:
///     a Register transaction commits even if the SMTP relay is offline.
///   - Caller-supplied <c>IdempotencyKey</c> makes retry safe — a handler
///     that fires twice (e.g. MediatR retry, browser double-submit) only
///     enqueues one row.
///   - Background processor handles retry / exponential backoff / max
///     attempts uniformly, so individual handlers don't reinvent it.
///
/// The implementation persists rows to the <c>email_outbox</c> table.
/// A dedicated background service drains the table and invokes the
/// existing <c>IEmailService</c> for actual SMTP delivery.
/// </summary>
public interface IEmailOutboxService
{
    /// <summary>
    /// Enqueue an email for asynchronous delivery. Returns once the row
    /// has been persisted; actual SMTP send happens out-of-band via the
    /// background processor.
    ///
    /// <para>
    /// <paramref name="idempotencyKey"/> is the caller's unique-per-business-event
    /// identifier (e.g. <c>"verify-email:{userId}"</c>,
    /// <c>"password-reset:{token}"</c>). The column is uniquely indexed —
    /// a duplicate insert is detected and treated as a no-op (the email
    /// is already enqueued or has been sent).
    /// </para>
    /// </summary>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="bodyHtml">Rendered HTML body.</param>
    /// <param name="idempotencyKey">
    /// Caller-supplied stable identifier. MUST be deterministic for the
    /// same business event so re-runs don't enqueue duplicates.
    /// </param>
    /// <param name="scheduledAt">
    /// Earliest UTC time at which the processor should attempt delivery.
    /// Defaults to "now" when null — useful for delayed reminders.
    /// </param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>
    /// True if a new row was inserted; false if a row with the same
    /// idempotency key already existed (caller can treat both as success).
    /// </returns>
    Task<bool> EnqueueAsync(
        string toEmail,
        string subject,
        string bodyHtml,
        string idempotencyKey,
        DateTime? scheduledAt = null,
        CancellationToken cancellationToken = default);
}
