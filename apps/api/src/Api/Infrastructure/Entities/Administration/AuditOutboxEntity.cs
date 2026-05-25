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

    public static AuditOutboxEntity CreatePending(string payloadJson, DateTimeOffset now)
        => new()
        {
            Id = Guid.NewGuid(),
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
}

public enum OutboxStatus { Pending = 0, Sent = 1, Failed = 2 }
