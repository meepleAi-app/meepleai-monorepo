namespace Api.Infrastructure.Entities;

public class AuditOutboxEntity
{
    public Guid Id { get; private set; }
    public string PayloadJson { get; private set; } = string.Empty;
    public OutboxStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    private AuditOutboxEntity() { }

    /// <summary>
    /// Creates a Pending outbox row whose <see cref="Id"/> is generated fresh here. This is the
    /// production path — <c>AuditService.EnqueueAuditAsync</c> / <c>EnqueueAuditAtomicAsync</c>
    /// call this overload. The T4b idempotency correlation is established later, by the processor:
    /// <c>AuditOutboxProcessor</c> materializes the audit_logs row with <c>audit_logs.Id == this.Id</c>,
    /// so a re-drain of the same outbox row finds the destination row already present and skips the
    /// re-insert. (The outbox Id is the idempotency key; callers do not pre-correlate it.)
    /// </summary>
    public static AuditOutboxEntity CreatePending(string payloadJson, DateTimeOffset now)
        => CreatePending(Guid.NewGuid(), payloadJson, now);

    /// <summary>
    /// Overload that accepts an explicit <paramref name="id"/>. Used by tests/seed fixtures that
    /// need to control the outbox row Id up front (e.g. idempotency tests that re-flag a known row
    /// Pending and assert no duplicate audit_logs row is produced). Production code uses the
    /// Id-generating overload above; the processor establishes the audit_logs correlation either way.
    /// </summary>
    public static AuditOutboxEntity CreatePending(Guid id, string payloadJson, DateTimeOffset now)
        => new()
        {
            Id = id,
            PayloadJson = payloadJson,
            Status = OutboxStatus.Pending,
            RetryCount = 0,
            CreatedAt = now,
        };

    public void MarkSent(DateTimeOffset now)
    {
        if (Status == OutboxStatus.Sent)
        {
            // Idempotent: at-least-once delivery may re-mark a Sent row.
            return;
        }
        if (Status == OutboxStatus.Failed)
        {
            // Retry path: Failed -> Sent is allowed (processor retries a failed row).
            // We preserve LastError + RetryCount so the operator can see the row failed before succeeding.
        }
        Status = OutboxStatus.Sent;
        ProcessedAt = now;
    }

    public void MarkFailed(string error, DateTimeOffset now)
    {
        if (Status == OutboxStatus.Sent)
        {
            throw new InvalidOperationException(
                $"Cannot mark a Sent outbox row {Id} as Failed; this would regress a successfully-processed audit. " +
                "Investigate the caller — likely a race in the processor.");
        }
        // Truncate the error message to fit the 2048-char column limit (defense in depth).
        var truncated = error.Length > 2048
            ? string.Concat(error.AsSpan(0, 2045), "...")
            : error;
        Status = OutboxStatus.Failed;
        RetryCount++;
        LastError = truncated;
        ProcessedAt = now;
    }

    /// <summary>
    /// Test-only helper to re-flag a Sent/Failed row as Pending. Used by
    /// <c>AuditOutboxIdempotencyTests</c> (SP5 S1 T4b step 4) to simulate the retry scenario
    /// the ON CONFLICT / pre-check idempotency must guard against. NOT for production use —
    /// the processor never reverts state on its own.
    /// </summary>
    internal void MarkPendingForTest()
    {
        Status = OutboxStatus.Pending;
        ProcessedAt = null;
    }
}

public enum OutboxStatus { Pending = 0, Sent = 1, Failed = 2 }
