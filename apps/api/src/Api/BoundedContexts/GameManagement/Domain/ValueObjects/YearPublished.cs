using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Value object representing the year a game was published.
/// </summary>
public sealed class YearPublished : ValueObject
{
    private const int MinYear = 1800; // Modern board games started ~1800s
    private static readonly int MaxYear = DateTime.UtcNow.Year + 5; // Allow future releases

    public int Value { get; }

    public YearPublished(int year)
    {
        if (year < MinYear)
            throw new ValidationException($"Publication year cannot be before {MinYear}");

        if (year > MaxYear)
            throw new ValidationException($"Publication year cannot be after {MaxYear}");

        Value = year;
    }

    /// <summary>
    /// Checks if game is a classic (published before 2000).
    /// </summary>
    public bool IsClassic => Value < 2000;

    /// <summary>
    /// Checks if game is modern (published 2000 or later).
    /// </summary>
    public bool IsModern => Value >= 2000;

    /// <summary>
    /// Checks if game is recent (published in last 3 years).
    /// </summary>
    public bool IsRecent => Value >= DateTime.UtcNow.Year - 3;

    /// <summary>
    /// Gets the age of the game in years.
    /// </summary>
    public int Age => DateTime.UtcNow.Year - Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator int(YearPublished year) => year.Value;
}
