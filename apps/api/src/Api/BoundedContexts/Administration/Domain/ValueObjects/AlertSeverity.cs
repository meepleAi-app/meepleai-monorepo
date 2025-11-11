using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Domain.ValueObjects;

public sealed class AlertSeverity : ValueObject
{
    public string Value { get; }

    public static readonly AlertSeverity Critical = new("critical");
    public static readonly AlertSeverity Warning = new("warning");
    public static readonly AlertSeverity Info = new("info");

    private AlertSeverity(string value)
    {
        Value = value;
    }

    public bool IsCritical => Value == Critical.Value;
    public bool IsWarning => Value == Warning.Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
