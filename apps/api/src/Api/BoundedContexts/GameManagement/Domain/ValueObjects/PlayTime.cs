using Api.SharedKernel.Domain;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Value object representing game play time range in minutes.
/// </summary>
public sealed class PlayTime : ValueObject
{
    private const int MinMinutes = 1;
    private const int MaxMinutes = 1440; // 24 hours

    public int MinMinutes { get; }
    public int MaxMinutes { get; }

    public PlayTime(int minMinutes, int maxMinutes)
    {
        if (minMinutes < MinMinutes)
            throw new ValidationException($"Minimum play time cannot be less than {MinMinutes} minute");

        if (maxMinutes > MaxMinutes)
            throw new ValidationException($"Maximum play time cannot exceed {MaxMinutes} minutes (24 hours)");

        if (minMinutes > maxMinutes)
            throw new ValidationException("Minimum play time cannot exceed maximum");

        MinMinutes = minMinutes;
        MaxMinutes = maxMinutes;
    }

    /// <summary>
    /// Gets average play time in minutes.
    /// </summary>
    public int AverageMinutes => (MinMinutes + MaxMinutes) / 2;

    /// <summary>
    /// Checks if game is quick (< 30 minutes).
    /// </summary>
    public bool IsQuick => MaxMinutes < 30;

    /// <summary>
    /// Checks if game is medium length (30-90 minutes).
    /// </summary>
    public bool IsMedium => MinMinutes >= 30 && MaxMinutes <= 90;

    /// <summary>
    /// Checks if game is long (> 90 minutes).
    /// </summary>
    public bool IsLong => MinMinutes > 90;

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
