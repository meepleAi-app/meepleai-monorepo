using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Value object representing the scoring configuration for a play record.
/// Defines which scoring dimensions are enabled and their display units.
/// </summary>
internal sealed class SessionScoringConfig : ValueObject
{
    public IReadOnlyList<string> EnabledDimensions { get; }
    public IReadOnlyDictionary<string, string> DimensionUnits { get; }

    public SessionScoringConfig(
        IEnumerable<string> enabledDimensions,
        IDictionary<string, string>? dimensionUnits = null)
    {
        ArgumentNullException.ThrowIfNull(enabledDimensions);

        var dimensions = enabledDimensions.ToList();
        if (dimensions.Count == 0)
            throw new ValidationException("At least one scoring dimension must be enabled");

        if (dimensions.Count > 10)
            throw new ValidationException("Maximum 10 scoring dimensions allowed");

        // Validate dimension names
        foreach (var dim in dimensions)
        {
            if (string.IsNullOrWhiteSpace(dim))
                throw new ValidationException("Scoring dimension cannot be empty");

            if (dim.Trim().Length > 50)
                throw new ValidationException("Scoring dimension cannot exceed 50 characters");
        }

        EnabledDimensions = dimensions.Select(d => d.Trim()).ToList().AsReadOnly();
        DimensionUnits = (dimensionUnits ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
            .ToDictionary(
                kvp => kvp.Key.Trim(),
                kvp => kvp.Value.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .AsReadOnly();
    }

    /// <summary>
    /// Creates default scoring configuration with single "points" dimension.
    /// </summary>
    public static SessionScoringConfig CreateDefault()
    {
        var units = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["points"] = "pts"
        };

        return new SessionScoringConfig(
            new List<string> { "points" },
            units);
    }

    /// <summary>
    /// Creates scoring configuration from a game's metadata.
    /// Falls back to default if game has no scoring metadata.
    /// Future: Extract from game.ScoringMetadata when schema is defined.
    /// </summary>
    public static SessionScoringConfig CreateFromGame(Entities.Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        // Future enhancement: Extract from game.ScoringMetadata when available
        return CreateDefault();
    }

    /// <summary>
    /// Checks if a dimension is enabled in this configuration.
    /// </summary>
    public bool HasDimension(string dimension) =>
        EnabledDimensions.Any(d => string.Equals(d, dimension, StringComparison.OrdinalIgnoreCase));

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var dim in EnabledDimensions.OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            yield return dim.ToLowerInvariant();

        foreach (var kvp in DimensionUnits.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
        {
            yield return kvp.Key.ToLowerInvariant();
            yield return kvp.Value.ToLowerInvariant();
        }
    }

    public override string ToString() =>
        $"Dimensions: [{string.Join(", ", EnabledDimensions)}]";
}
