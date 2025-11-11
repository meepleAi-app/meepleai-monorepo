using Api.SharedKernel.Domain;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Value object representing a game publisher name.
/// </summary>
public sealed class Publisher : ValueObject
{
    private const int MaxLength = 100;

    public string Name { get; }

    public Publisher(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Publisher name cannot be empty");

        var trimmed = name.Trim();

        if (trimmed.Length > MaxLength)
            throw new ValidationException($"Publisher name cannot exceed {MaxLength} characters");

        Name = trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name.ToLowerInvariant();
    }

    public override string ToString() => Name;

    public static implicit operator string(Publisher publisher) => publisher.Name;
}
