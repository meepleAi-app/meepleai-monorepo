namespace Api.SharedKernel.Domain.ValueObjects;

/// <summary>
/// Value object representing a percentage value (0-100).
/// Provides type-safe percentage handling with automatic validation.
/// </summary>
/// <remarks>
/// This value object enforces the domain constraint that percentages
/// must be between 0 and 100 inclusive, eliminating duplicate validation
/// across multiple value objects (E2EMetrics, PerformanceMetrics, AccessibilityMetrics).
///
/// Usage:
/// <code>
/// var coverage = Percentage.Create(95.5m);
/// decimal rawValue = coverage; // implicit conversion
/// </code>
/// </remarks>
internal sealed class Percentage : ValueObject
{
    /// <summary>
    /// The percentage value (0-100).
    /// </summary>
    public decimal Value { get; }

    private Percentage(decimal value) => Value = value;

    /// <summary>
    /// Creates a new Percentage instance with validation.
    /// </summary>
    /// <param name="value">The percentage value (must be between 0 and 100)</param>
    /// <returns>A validated Percentage instance</returns>
    /// <exception cref="ArgumentOutOfRangeException">When value is outside 0-100 range</exception>
    public static Percentage Create(decimal value)
    {
        if (value < 0 || value > 100)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Percentage must be between 0 and 100");

        return new Percentage(value);
    }

    /// <summary>
    /// Creates a Percentage from a ratio (0.0-1.0), converting to percentage (0-100).
    /// </summary>
    /// <param name="ratio">A ratio value between 0.0 and 1.0</param>
    /// <returns>A Percentage representing the ratio as a percentage</returns>
    /// <exception cref="ArgumentOutOfRangeException">When ratio is outside 0.0-1.0 range</exception>
    public static Percentage FromRatio(decimal ratio)
    {
        if (ratio < 0 || ratio > 1)
            throw new ArgumentOutOfRangeException(nameof(ratio), ratio, "Ratio must be between 0.0 and 1.0");

        return new Percentage(ratio * 100);
    }

    /// <summary>
    /// Creates a Percentage from a fraction (count/total).
    /// </summary>
    /// <param name="count">The numerator (items of interest)</param>
    /// <param name="total">The denominator (total items)</param>
    /// <returns>The percentage, or Zero if total is 0</returns>
    public static Percentage FromFraction(int count, int total)
    {
        if (total <= 0)
            return Zero;

        var percentage = (decimal)count / total * 100;
        return new Percentage(Math.Clamp(percentage, 0, 100));
    }

    /// <summary>
    /// A Percentage representing 0%.
    /// </summary>
    public static Percentage Zero => new(0);

    /// <summary>
    /// A Percentage representing 100%.
    /// </summary>
    public static Percentage OneHundred => new(100);

    /// <summary>
    /// Converts the percentage to a ratio (0.0-1.0).
    /// </summary>
    public decimal ToRatio() => Value / 100;

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Value:F1}%";

    /// <summary>
    /// Returns a formatted string with the specified number of decimal places.
    /// </summary>
    /// <param name="decimalPlaces">Number of decimal places</param>
    public string ToString(int decimalPlaces) => $"{Value.ToString($"F{decimalPlaces}")}%";

    /// <summary>
    /// Implicit conversion to decimal for use in calculations.
    /// </summary>
    public static implicit operator decimal(Percentage percentage) => percentage.Value;

    // Comparison operators
    public static bool operator >(Percentage left, decimal right) => left.Value > right;
    public static bool operator <(Percentage left, decimal right) => left.Value < right;
    public static bool operator >=(Percentage left, decimal right) => left.Value >= right;
    public static bool operator <=(Percentage left, decimal right) => left.Value <= right;
    public static bool operator >(Percentage left, Percentage right) => left.Value > right.Value;
    public static bool operator <(Percentage left, Percentage right) => left.Value < right.Value;
    public static bool operator >=(Percentage left, Percentage right) => left.Value >= right.Value;
    public static bool operator <=(Percentage left, Percentage right) => left.Value <= right.Value;
}
