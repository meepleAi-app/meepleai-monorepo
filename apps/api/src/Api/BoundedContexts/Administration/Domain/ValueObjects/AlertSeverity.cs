using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Domain.ValueObjects;

internal sealed class AlertSeverity : ValueObject
{
    public string Value { get; }

    public static readonly AlertSeverity Critical = new("critical");
    public static readonly AlertSeverity Warning = new("warning");
    public static readonly AlertSeverity Info = new("info");

    private AlertSeverity(string value)
    {
        Value = value;
    }

    public bool IsCritical => string.Equals(Value, Critical.Value, StringComparison.Ordinal);
    public bool IsWarning => string.Equals(Value, Warning.Value, StringComparison.Ordinal);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
