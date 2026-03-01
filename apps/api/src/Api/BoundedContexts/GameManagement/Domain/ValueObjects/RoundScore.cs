using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Value object representing a score entry for a specific round and dimension.
/// </summary>
internal sealed class RoundScore : ValueObject
{
    private const int MaxDimensionLength = 50;
    private const int MaxUnitLength = 20;

    public Guid PlayerId { get; }
    public int Round { get; }
    public string Dimension { get; }
    public int Value { get; }
    public string? Unit { get; }
    public DateTime RecordedAt { get; }

    public RoundScore(
        Guid playerId,
        int round,
        string dimension,
        int value,
        DateTime recordedAt,
        string? unit = null)
    {
        if (playerId == Guid.Empty)
            throw new ValidationException("Player ID cannot be empty");

        if (round < 1)
            throw new ValidationException("Round must be at least 1");

        if (string.IsNullOrWhiteSpace(dimension))
            throw new ValidationException("Score dimension cannot be empty");

        var trimmedDimension = dimension.Trim();
        if (trimmedDimension.Length > MaxDimensionLength)
            throw new ValidationException($"Score dimension cannot exceed {MaxDimensionLength} characters");

        if (unit != null && unit.Trim().Length > MaxUnitLength)
            throw new ValidationException($"Score unit cannot exceed {MaxUnitLength} characters");

        PlayerId = playerId;
        Round = round;
        Dimension = trimmedDimension;
        Value = value;
        Unit = unit?.Trim();
        RecordedAt = recordedAt;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return PlayerId;
        yield return Round;
        yield return Dimension.ToLowerInvariant();
        yield return Value;
    }

    public override string ToString() =>
        Unit != null
            ? $"R{Round} {Dimension}: {Value} {Unit} (Player: {PlayerId})"
            : $"R{Round} {Dimension}: {Value} (Player: {PlayerId})";
}
