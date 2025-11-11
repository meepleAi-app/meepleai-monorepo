using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Represents a confidence score value object.
/// Ensures confidence is between 0.0 and 1.0.
/// </summary>
public sealed class Confidence : ValueObject
{
    public double Value { get; }

    public Confidence(double value)
    {
        if (value < 0.0 || value > 1.0)
            throw new ValidationException(nameof(Confidence), $"Confidence must be between 0.0 and 1.0, got {value}");

        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ValidationException(nameof(Confidence), "Confidence cannot be NaN or Infinity");

        Value = value;
    }

    public bool IsHigh() => Value >= 0.8;
    public bool IsMedium() => Value >= 0.5 && Value < 0.8;
    public bool IsLow() => Value < 0.5;

    public static Confidence High => new(0.9);
    public static Confidence Medium => new(0.7);
    public static Confidence Low => new(0.3);
    public static Confidence Zero => new(0.0);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => $"{Value:P0}"; // Format as percentage

    public static implicit operator double(Confidence confidence) => confidence.Value;
    public static Confidence Parse(double value) => new(value);
}
