using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

/// <summary>
/// Value object representing notification severity level.
/// Determines UI presentation and user attention priority.
/// </summary>
public sealed class NotificationSeverity : ValueObject
{
    public string Value { get; }

    public static readonly NotificationSeverity Info = new("info");
    public static readonly NotificationSeverity Success = new("success");
    public static readonly NotificationSeverity Warning = new("warning");
    public static readonly NotificationSeverity Error = new("error");

    private NotificationSeverity(string value)
    {
        Value = value;
    }

    public bool IsInfo => string.Equals(Value, Info.Value, StringComparison.Ordinal);
    public bool IsSuccess => string.Equals(Value, Success.Value, StringComparison.Ordinal);
    public bool IsWarning => string.Equals(Value, Warning.Value, StringComparison.Ordinal);
    public bool IsError => string.Equals(Value, Error.Value, StringComparison.Ordinal);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    /// <summary>
    /// Creates a NotificationSeverity from a string value.
    /// Validates against known severity levels.
    /// </summary>
    public static NotificationSeverity FromString(string value)
    {
        var normalized = value.ToLowerInvariant();
        return normalized switch
        {
            "info" => Info,
            "success" => Success,
            "warning" => Warning,
            "error" => Error,
            _ => throw new ArgumentException($"Unknown notification severity: {value}", nameof(value))
        };
    }
}
