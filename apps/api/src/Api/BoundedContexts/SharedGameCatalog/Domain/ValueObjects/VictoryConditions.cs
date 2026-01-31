using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Value object representing victory conditions in a board game.
/// </summary>
public sealed class VictoryConditions : ValueObject
{
    /// <summary>
    /// Gets the primary victory condition description.
    /// </summary>
    public string Primary { get; private set; }

    /// <summary>
    /// Gets the list of alternative victory conditions.
    /// </summary>
    public List<string> Alternatives { get; private set; }

    /// <summary>
    /// Gets whether the game has a point-based victory system.
    /// </summary>
    public bool IsPointBased { get; private set; }

    /// <summary>
    /// Gets the target points required for victory (if point-based).
    /// </summary>
    public int? TargetPoints { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private VictoryConditions()
    {
        Primary = string.Empty;
        Alternatives = new List<string>();
        IsPointBased = false;
    }

    /// <summary>
    /// Private constructor for creating victory conditions.
    /// </summary>
    private VictoryConditions(
        string primary,
        List<string> alternatives,
        bool isPointBased,
        int? targetPoints)
    {
        Primary = primary;
        Alternatives = alternatives;
        IsPointBased = isPointBased;
        TargetPoints = targetPoints;
    }

    /// <summary>
    /// Creates a new VictoryConditions instance with validation.
    /// </summary>
    public static VictoryConditions Create(
        string primary,
        List<string>? alternatives = null,
        bool isPointBased = false,
        int? targetPoints = null)
    {
        if (string.IsNullOrWhiteSpace(primary))
            throw new ArgumentException("Primary victory condition is required", nameof(primary));

        if (isPointBased && targetPoints is <= 0)
            throw new ArgumentException("Target points must be positive if point-based", nameof(targetPoints));

        return new VictoryConditions(
            primary.Trim(),
            alternatives ?? new List<string>(),
            isPointBased,
            targetPoints);
    }

    /// <summary>
    /// Creates an empty VictoryConditions for games without clear victory conditions.
    /// </summary>
    public static VictoryConditions Empty => new(
        "Not specified",
        new List<string>(),
        false,
        null);

    /// <inheritdoc/>
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Primary;
        yield return IsPointBased;
        if (TargetPoints.HasValue)
            yield return TargetPoints.Value;
        foreach (var alternative in Alternatives)
            yield return alternative;
    }
}
