using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

/// <summary>
/// Collection name value object for document collections.
/// Issue #2051: Validates collection naming conventions
/// </summary>
public sealed class CollectionName : ValueObject
{
    private const int MaxLength = 200;
    private const int MinLength = 3;

    public string Value { get; }

    public CollectionName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException(nameof(CollectionName), "Collection name cannot be empty");

        var trimmed = name.Trim();

        if (trimmed.Length < MinLength)
            throw new ValidationException(nameof(CollectionName),
                $"Collection name must be at least {MinLength} characters");

        if (trimmed.Length > MaxLength)
            throw new ValidationException(nameof(CollectionName),
                $"Collection name cannot exceed {MaxLength} characters");

        Value = trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
