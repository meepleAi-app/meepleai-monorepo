namespace Api.BoundedContexts.UserNotifications.Infrastructure.Entities;

/// <summary>
/// Persistence model for the email outbox pattern (I5 prep).
/// Distinct from <c>EmailQueueEntity</c> (transactional mail queue with retry policy):
/// the outbox stores caller-supplied <see cref="IdempotencyKey"/> values so that
/// the same business event (e.g., admin invitation issued) cannot trigger duplicate
/// emails when handlers retry.
/// </summary>
public class EmailOutboxEntity
{
    public Guid Id { get; init; }

    public string ToEmail { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string BodyHtml { get; init; } = string.Empty;

    /// <summary>
    /// Caller-supplied idempotency key. Unique across the outbox table; duplicates are
    /// rejected at insert time so handlers can safely retry without sending duplicate mail.
    /// </summary>
    public string IdempotencyKey { get; init; } = string.Empty;

    /// <summary>
    /// Earliest time at which the outbox processor should attempt delivery (UTC).
    /// </summary>
    public DateTime ScheduledAt { get; init; }

    /// <summary>
    /// Time at which the email was successfully dispatched, or null if pending/failed.
    /// </summary>
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// Number of dispatch attempts so far (used by retry/backoff logic).
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Last error message captured during dispatch, or null on success.
    /// </summary>
    public string? LastError { get; set; }

    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Lifecycle status. One of: Pending | Sent | FailedPermanent.
    /// </summary>
    public string Status { get; set; } = "Pending";
}
