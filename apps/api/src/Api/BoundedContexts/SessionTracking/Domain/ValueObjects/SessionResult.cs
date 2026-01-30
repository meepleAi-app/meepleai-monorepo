namespace Api.BoundedContexts.SessionTracking.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing final session results.
/// </summary>
public record SessionResult
{
    /// <summary>
    /// Winner participant ID (highest score).
    /// </summary>
    public Guid WinnerId { get; init; }

    /// <summary>
    /// Final ranks for all participants (ParticipantId -> Rank).
    /// </summary>
    public IReadOnlyDictionary<Guid, int> FinalRanks { get; init; } = new Dictionary<Guid, int>();

    /// <summary>
    /// Session statistics (total rounds, duration, etc.).
    /// </summary>
    public SessionStatistics Statistics { get; init; } = SessionStatistics.Empty;

    /// <summary>
    /// Private constructor for validation.
    /// </summary>
    private SessionResult() { }

    /// <summary>
    /// Factory method to create session result.
    /// </summary>
    /// <param name="winnerId">Winner participant ID.</param>
    /// <param name="finalRanks">Final ranks dictionary.</param>
    /// <param name="statistics">Session statistics.</param>
    /// <returns>Validated SessionResult instance.</returns>
    public static SessionResult Create(
        Guid winnerId,
        IReadOnlyDictionary<Guid, int> finalRanks,
        SessionStatistics statistics)
    {
        if (winnerId == Guid.Empty)
            throw new ArgumentException("Winner ID cannot be empty.", nameof(winnerId));

        ArgumentNullException.ThrowIfNull(finalRanks);
        ArgumentNullException.ThrowIfNull(statistics);

        if (finalRanks.Count == 0)
            throw new ArgumentException("Final ranks cannot be empty.", nameof(finalRanks));

        if (!finalRanks.ContainsKey(winnerId))
            throw new ArgumentException("Winner ID must be in final ranks.", nameof(winnerId));

        if (finalRanks[winnerId] != 1)
            throw new ArgumentException("Winner must have rank 1.", nameof(winnerId));

        // Validate ranks are sequential starting from 1
        var ranks = finalRanks.Values.OrderBy(r => r).ToList();
        for (int i = 0; i < ranks.Count; i++)
        {
            if (ranks[i] != i + 1)
                throw new ArgumentException("Ranks must be sequential starting from 1.", nameof(finalRanks));
        }

        return new SessionResult
        {
            WinnerId = winnerId,
            FinalRanks = finalRanks,
            Statistics = statistics
        };
    }
}

/// <summary>
/// Immutable value object for session statistics.
/// </summary>
public record SessionStatistics
{
    /// <summary>
    /// Total number of rounds played.
    /// </summary>
    public int TotalRounds { get; init; }

    /// <summary>
    /// Session duration in minutes.
    /// </summary>
    public int DurationMinutes { get; init; }

    /// <summary>
    /// Total number of score entries.
    /// </summary>
    public int TotalScoreEntries { get; init; }

    /// <summary>
    /// Total number of notes created.
    /// </summary>
    public int TotalNotes { get; init; }

    /// <summary>
    /// Empty statistics instance.
    /// </summary>
    public static SessionStatistics Empty => new()
    {
        TotalRounds = 0,
        DurationMinutes = 0,
        TotalScoreEntries = 0,
        TotalNotes = 0
    };

    /// <summary>
    /// Factory method to create session statistics.
    /// </summary>
    public static SessionStatistics Create(
        int totalRounds,
        int durationMinutes,
        int totalScoreEntries,
        int totalNotes)
    {
        if (totalRounds < 0)
            throw new ArgumentException("Total rounds cannot be negative.", nameof(totalRounds));

        if (durationMinutes < 0)
            throw new ArgumentException("Duration cannot be negative.", nameof(durationMinutes));

        if (totalScoreEntries < 0)
            throw new ArgumentException("Total score entries cannot be negative.", nameof(totalScoreEntries));

        if (totalNotes < 0)
            throw new ArgumentException("Total notes cannot be negative.", nameof(totalNotes));

        return new SessionStatistics
        {
            TotalRounds = totalRounds,
            DurationMinutes = durationMinutes,
            TotalScoreEntries = totalScoreEntries,
            TotalNotes = totalNotes
        };
    }
}