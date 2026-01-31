using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Value object representing the status of a game session.
/// </summary>
internal sealed class SessionStatus : ValueObject
{
    public string Value { get; }

    // Known session statuses
    public static readonly SessionStatus Setup = new("Setup");
    public static readonly SessionStatus InProgress = new("InProgress");
    public static readonly SessionStatus Paused = new("Paused");
    public static readonly SessionStatus Completed = new("Completed");
    public static readonly SessionStatus Abandoned = new("Abandoned");

    private SessionStatus(string value)
    {
        Value = value;
    }

    public bool IsActive => string.Equals(Value, InProgress.Value, StringComparison.Ordinal) || string.Equals(Value, Setup.Value, StringComparison.Ordinal) || string.Equals(Value, Paused.Value, StringComparison.Ordinal);
    public bool IsFinished => string.Equals(Value, Completed.Value, StringComparison.Ordinal) || string.Equals(Value, Abandoned.Value, StringComparison.Ordinal);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(SessionStatus status) => status.Value;
}
