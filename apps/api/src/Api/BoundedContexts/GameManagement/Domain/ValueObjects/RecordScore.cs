using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Value object representing a multi-dimensional score in a play record.
/// Supports various scoring schemes: points, ranking, wins, custom dimensions.
/// </summary>
internal sealed class RecordScore : ValueObject
{
    public string Dimension { get; }
    public int Value { get; }
    public string? Unit { get; }

    public RecordScore(string dimension, int value, string? unit = null)
    {
        if (string.IsNullOrWhiteSpace(dimension))
            throw new ValidationException("Score dimension cannot be empty");

        var trimmed = dimension.Trim();
        if (trimmed.Length > 50)
            throw new ValidationException("Score dimension cannot exceed 50 characters");

        if (value < 0)
            throw new ValidationException("Score value cannot be negative");

        Dimension = trimmed;
        Value = value;
        Unit = unit?.Trim();
    }

    /// <summary>
    /// Creates a points-based score.
    /// </summary>
    public static RecordScore Points(int points) =>
        new("points", points, "pts");

    /// <summary>
    /// Creates a ranking-based score (1st, 2nd, 3rd, etc.).
    /// </summary>
    public static RecordScore Ranking(int rank) =>
        new("ranking", rank, $"{rank}º");

    /// <summary>
    /// Creates a wins-based score.
    /// </summary>
    public static RecordScore Wins(int wins) =>
        new("wins", wins, "W");

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Dimension.ToLowerInvariant();
        yield return Value;
        yield return Unit?.ToLowerInvariant();
    }

    public override string ToString() =>
        Unit != null ? $"{Value} {Unit}" : $"{Value}";
}
