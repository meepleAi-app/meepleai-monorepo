using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Thread status value object representing the lifecycle state of a chat thread.
/// </summary>
internal sealed class ThreadStatus : IEquatable<ThreadStatus>
{
    public string Value { get; }

    public static readonly ThreadStatus Active = new("active");
    public static readonly ThreadStatus Closed = new("closed");

    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "active",
        "closed"
    };

    private ThreadStatus(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a ThreadStatus from a string value.
    /// </summary>
    public static ThreadStatus From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Thread status cannot be empty");

        var normalized = value.Trim().ToLowerInvariant();

        if (!ValidStatuses.Contains(normalized))
            throw new DomainException($"Invalid thread status: {value}. Valid values: active, closed");

        return normalized switch
        {
            "active" => Active,
            "closed" => Closed,
            _ => throw new DomainException($"Invalid thread status: {value}")
        };
    }

    /// <summary>
    /// Checks if the thread is active.
    /// </summary>
    public bool IsActive => string.Equals(Value, "active", StringComparison.Ordinal);

    /// <summary>
    /// Checks if the thread is closed.
    /// </summary>
    public bool IsClosed => string.Equals(Value, "closed", StringComparison.Ordinal);

    public bool Equals(ThreadStatus? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        return obj is ThreadStatus other && Equals(other);
    }

    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
    }

    public override string ToString() => Value;

    public static bool operator ==(ThreadStatus? left, ThreadStatus? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ThreadStatus? left, ThreadStatus? right)
    {
        return !Equals(left, right);
    }

    public static implicit operator string(ThreadStatus status) => status.Value;
}
