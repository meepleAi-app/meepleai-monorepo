using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Value object representing an FAQ answer.
/// </summary>
internal sealed class FAQAnswer : ValueObject
{
    public string Value { get; }

    public FAQAnswer(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("FAQ answer cannot be empty", nameof(value));

        if (value.Length > 5000)
            throw new ArgumentException("FAQ answer cannot exceed 5000 characters", nameof(value));

        Value = value.Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
