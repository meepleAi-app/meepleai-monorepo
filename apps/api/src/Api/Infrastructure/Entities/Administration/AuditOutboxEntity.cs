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
        Status = OutboxStatus.Sent;
        ProcessedAt = now;
    }

    public void MarkFailed(string error, DateTimeOffset now)
    {
        Status = OutboxStatus.Failed;
        RetryCount++;
        LastError = error;
        ProcessedAt = now;
    }
}

public enum OutboxStatus { Pending = 0, Sent = 1, Failed = 2 }
