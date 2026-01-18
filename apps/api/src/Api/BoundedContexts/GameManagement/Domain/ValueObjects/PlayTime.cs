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
    /// <summary>
    /// Gets the minimum play time in minutes for this game.
    /// </summary>
    public int MinMinutes { get; }

    /// <summary>
    /// Gets the maximum play time in minutes for this game.
    /// </summary>
    public int MaxMinutes { get; }

    /// <summary>
    /// Creates a new instance of PlayTime with specified min and max duration.
    /// </summary>
    /// <param name="minMinutes">Minimum play time in minutes (must be between 1 and 1440).</param>
    /// <param name="maxMinutes">Maximum play time in minutes (must be between 1 and 1440, and &gt;= minMinutes).</param>
    /// <exception cref="ValidationException">Thrown when minMinutes or maxMinutes are out of valid range, or maxMinutes &lt; minMinutes.</exception>
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
    /// <remarks>
    /// Quick games (&lt;= 30 minutes) are ideal for casual play sessions, filler games between longer titles,
    /// or introducing new players to board gaming. Threshold set at 30 minutes based on user session
    /// duration analysis and typical "coffee break" game length expectations.
    /// Examples: Love Letter, Codenames, Splendor.
    /// </remarks>
    /// <example>
    /// <code>
    /// var playTime = new PlayTime(15, 25);
    /// if (playTime.IsQuick)
    ///     logger.LogInformation("Quick game suitable for casual sessions: {Time}", playTime);
    /// </code>
    /// </example>
    public bool IsQuick => MaxMinutes <= PlayTimeCategories.QuickGameThreshold;

    /// <summary>
    /// Checks if game is medium length (30-90 minutes).
    /// </summary>
    /// <remarks>
    /// Medium-length games (30-90 minutes) represent the most common play time category for modern board games.
    /// These games balance strategic depth with reasonable time commitment, ideal for dedicated game nights.
    /// Threshold based on BoardGameGeek data showing peak game length distribution and player preference surveys.
    /// Examples: Catan, Ticket to Ride, Azul.
    /// </remarks>
    /// <example>
    /// <code>
    /// var playTime = new PlayTime(45, 60);
    /// if (playTime.IsMedium)
    ///     logger.LogInformation("Medium-length game ideal for game nights: {Time}", playTime);
    /// </code>
    /// </example>
    public bool IsMedium => MinMinutes >= PlayTimeCategories.QuickGameThreshold && MaxMinutes <= PlayTimeCategories.MediumGameThreshold;

    /// <summary>
    /// Checks if game is long (&gt; 90 minutes).
    /// </summary>
    /// <remarks>
    /// Long games (&gt; 90 minutes) require significant time commitment and are typically reserved for
    /// dedicated gaming sessions with experienced players. Threshold set at 90 minutes to identify games
    /// requiring special session planning and player commitment expectations.
    /// These games often feature complex mechanics and epic narratives.
    /// Examples: Twilight Imperium, Gloomhaven, Wingspan (with expansions).
    /// </remarks>
    /// <example>
    /// <code>
    /// var playTime = new PlayTime(120, 180);
    /// if (playTime.IsLong)
    ///     logger.LogWarning("Long game requires dedicated session planning: {Time}", playTime);
    /// </code>
    /// </example>
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
