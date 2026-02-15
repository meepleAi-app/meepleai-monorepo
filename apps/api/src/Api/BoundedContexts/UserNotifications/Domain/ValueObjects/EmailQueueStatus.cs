using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

/// <summary>
/// Value object representing the status of an email queue item.
/// Issue #4417: Email notification queue with retry policy.
/// </summary>
internal sealed class EmailQueueStatus : ValueObject
{
    public string Value { get; }

    public static readonly EmailQueueStatus Pending = new("pending");
    public static readonly EmailQueueStatus Processing = new("processing");
    public static readonly EmailQueueStatus Sent = new("sent");
    public static readonly EmailQueueStatus Failed = new("failed");
    public static readonly EmailQueueStatus DeadLetter = new("dead_letter");

    private EmailQueueStatus(string value)
    {
        Value = value;
    }

    public bool IsPending => string.Equals(Value, Pending.Value, StringComparison.Ordinal);
    public bool IsProcessing => string.Equals(Value, Processing.Value, StringComparison.Ordinal);
    public bool IsSent => string.Equals(Value, Sent.Value, StringComparison.Ordinal);
    public bool IsFailed => string.Equals(Value, Failed.Value, StringComparison.Ordinal);
    public bool IsDeadLetter => string.Equals(Value, DeadLetter.Value, StringComparison.Ordinal);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    /// <summary>
    /// Creates an EmailQueueStatus from a string value.
    /// Validates against known statuses.
    /// </summary>
    public static EmailQueueStatus FromString(string value)
    {
        var normalized = value.ToLowerInvariant();
        return normalized switch
        {
            "pending" => Pending,
            "processing" => Processing,
            "sent" => Sent,
            "failed" => Failed,
            "dead_letter" => DeadLetter,
            _ => throw new ArgumentException($"Unknown email queue status: {value}", nameof(value))
        };
    }
}
