using Api.BoundedContexts.SessionTracking.Domain.Enums;

namespace Api.BoundedContexts.SessionTracking.Domain.Scoring;

/// <summary>
/// DEC-1 stub: Ranking-based scoring strategy (filled in T8).
/// Handles per-player position 1..N (racing games, time trials).
/// </summary>
public sealed class RankingScoringStrategy : IScoringStrategy
{
    public ScoreType Type => ScoreType.Ranking;

    public ScoringValidationResult Validate(string scoreDataJson) =>
        throw new NotSupportedException("Filled in T8");

    public string Serialize(object scoreData) =>
        throw new NotSupportedException("Filled in T8");

    public object Deserialize(string scoreDataJson) =>
        throw new NotSupportedException("Filled in T8");

    public Guid? ComputeWinnerPlayerId(string scoreDataJson) =>
        throw new NotSupportedException("Filled in T8");
}
