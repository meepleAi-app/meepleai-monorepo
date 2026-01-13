using Api.SharedKernel.Constants;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Guards;

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Value object representing game play time range in minutes.
/// </summary>
internal sealed class PlayTime : ValueObject
{
    public int MinMinutes { get; }
    public int MaxMinutes { get; }

    public PlayTime(int minMinutes, int maxMinutes)
    {
        Guard.AgainstOutOfRange(minMinutes, nameof(minMinutes), PlayTimeCategories.MinimumPlayTime, PlayTimeCategories.MaximumPlayTime);
        Guard.AgainstOutOfRange(maxMinutes, nameof(maxMinutes), PlayTimeCategories.MinimumPlayTime, PlayTimeCategories.MaximumPlayTime);
        Guard.AgainstInvalidRange(minMinutes, maxMinutes, nameof(minMinutes), nameof(maxMinutes));

        MinMinutes = minMinutes;
        MaxMinutes = maxMinutes;
    }

    /// <summary>
    /// Gets average play time in minutes.
    /// </summary>
    public int AverageMinutes => (MinMinutes + MaxMinutes) / 2;

    /// <summary>
    /// Checks if game is quick (&lt;= 30 minutes).
    /// </summary>
    public bool IsQuick => MaxMinutes <= PlayTimeCategories.QuickGameThreshold;

    /// <summary>
    /// Checks if game is medium length (30-90 minutes).
    /// </summary>
    public bool IsMedium => MinMinutes >= PlayTimeCategories.QuickGameThreshold && MaxMinutes <= PlayTimeCategories.MediumGameThreshold;

    /// <summary>
    /// Checks if game is long (> 90 minutes).
    /// </summary>
    public bool IsLong => MinMinutes > PlayTimeCategories.MediumGameThreshold;

    /// <summary>
    /// Creates play time for quick games (15-30 min).
    /// </summary>
    public static PlayTime Quick => new PlayTime(15, 30);

    /// <summary>
    /// Creates play time for standard games (45-60 min).
    /// </summary>
    public static PlayTime Standard => new PlayTime(45, 60);

    /// <summary>
    /// Creates play time for long games (120-180 min).
    /// </summary>
    public static PlayTime Long => new PlayTime(120, 180);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MinMinutes;
        yield return MaxMinutes;
    }

    public override string ToString() =>
        MinMinutes == MaxMinutes
            ? $"{MinMinutes} min"
            : $"{MinMinutes}-{MaxMinutes} min";
}
