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
    /// Creates a Pending outbox row with the given <paramref name="id"/>. The caller (the
    /// AuditLoggingBehavior atomic path) generates this id and passes the SAME value as
    /// <see cref="AuditLogEntity.Id"/> when the processor later materializes the row — this
    /// is the basis for T4b idempotency: a retried materialization is skipped because the
    /// destination audit_logs row already exists with this id.
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

    /// <summary>
    /// Convenience overload that generates a fresh Guid. Retained for callers that do not need
    /// to correlate the outbox row with the materialized audit_logs row (e.g. seed/test fixtures
    /// for non-idempotency scenarios).
    /// </summary>
    public static AuditOutboxEntity CreatePending(string payloadJson, DateTimeOffset now)
        => CreatePending(Guid.NewGuid(), payloadJson, now);

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
