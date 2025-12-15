using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Value object representing an FAQ question.
/// </summary>
internal sealed class FAQQuestion : ValueObject
{
    public string Value { get; }

    public FAQQuestion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("FAQ question cannot be empty", nameof(value));

        if (value.Length > 500)
            throw new ArgumentException("FAQ question cannot exceed 500 characters", nameof(value));

        Value = value.Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
