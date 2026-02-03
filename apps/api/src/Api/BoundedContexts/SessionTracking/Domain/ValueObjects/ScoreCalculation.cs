namespace Api.BoundedContexts.SessionTracking.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing score calculations for a participant.
/// </summary>
public record ScoreCalculation
{
    /// <summary>
    /// Participant identifier.
    /// </summary>
    public Guid ParticipantId { get; init; }

    /// <summary>
    /// Total score across all rounds/categories.
    /// </summary>
    public decimal TotalScore { get; init; }

    /// <summary>
    /// Average score per round (if applicable).
    /// </summary>
    public decimal? AverageScore { get; init; }

    /// <summary>
    /// Rank among all participants (1 = highest score).
    /// </summary>
    public int Rank { get; init; }

    /// <summary>
    /// Number of score entries for this participant.
    /// </summary>
    public int EntryCount { get; init; }

    /// <summary>
    /// Private constructor for validation.
    /// </summary>
    private ScoreCalculation() { }

    /// <summary>
    /// Factory method to create score calculation from score entries.
    /// </summary>
    /// <param name="participantId">Participant identifier.</param>
    /// <param name="scores">Collection of score values.</param>
    /// <param name="rank">Rank among participants.</param>
    /// <returns>Validated ScoreCalculation instance.</returns>
    public static ScoreCalculation Create(Guid participantId, IEnumerable<decimal> scores, int rank)
    {
        if (participantId == Guid.Empty)
            throw new ArgumentException("Participant ID cannot be empty.", nameof(participantId));

        if (rank <= 0)
            throw new ArgumentException("Rank must be positive.", nameof(rank));

        ArgumentNullException.ThrowIfNull(scores);

        var scoreList = scores.ToList();
        var total = scoreList.Sum();
        var average = scoreList.Count > 0 ? total / scoreList.Count : (decimal?)null;

        return new ScoreCalculation
        {
            ParticipantId = participantId,
            TotalScore = total,
            AverageScore = average,
            Rank = rank,
            EntryCount = scoreList.Count
        };
    }

    /// <summary>
    /// Calculates total score from a collection of scores.
    /// </summary>
    public static decimal CalculateTotal(IEnumerable<decimal> scores)
    {
        ArgumentNullException.ThrowIfNull(scores);
        return scores.Sum();
    }

    /// <summary>
    /// Calculates average score from a collection of scores.
    /// </summary>
    public static decimal? CalculateAverage(IEnumerable<decimal> scores)
    {
        ArgumentNullException.ThrowIfNull(scores);
        var scoreList = scores.ToList();
        return scoreList.Count > 0 ? scoreList.Average() : (decimal?)null;
    }

    /// <summary>
    /// Determines rank based on total score (higher is better).
    /// </summary>
    public static IEnumerable<(Guid ParticipantId, int Rank)> CalculateRanks(
        IEnumerable<(Guid ParticipantId, decimal TotalScore)> participantScores)
    {
        ArgumentNullException.ThrowIfNull(participantScores);

        return participantScores
            .OrderByDescending(p => p.TotalScore)
            .Select((p, index) => (p.ParticipantId, Rank: index + 1))
            .ToList();
    }
}