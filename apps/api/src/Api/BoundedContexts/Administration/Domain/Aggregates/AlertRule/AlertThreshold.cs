namespace Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;

public record AlertThreshold
{
    public double Value { get; init; }
    public string Unit { get; init; }

    public AlertThreshold(double value, string unit)
    {
        if (value < 0) throw new ArgumentException("Threshold value cannot be negative", nameof(value));
        if (string.IsNullOrWhiteSpace(unit)) throw new ArgumentException("Unit cannot be empty", nameof(unit));
        Value = value;
        Unit = unit;
    }

    public bool IsExceeded(double metricValue) => metricValue >= Value;
    public override string ToString() => $"{Value} {Unit}";
    
    public static AlertThreshold Percentage(double value) => new(value, "%");
    public static AlertThreshold Milliseconds(double value) => new(value, "ms");
    public static AlertThreshold Count(double value) => new(value, "count");
}
