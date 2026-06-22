using Api.BoundedContexts.SessionTracking.Domain.Enums;

namespace Api.BoundedContexts.SessionTracking.Domain.Scoring;

/// <summary>
/// DEC-1: Factory dispatch for IScoringStrategy implementations based on ScoreType.
/// Used by SaveSessionCommandValidator (T10) and SessionScoresUpdated event handlers.
/// </summary>
public sealed class ScoringStrategyFactory
{
    public IScoringStrategy GetStrategy(ScoreType type) => type switch
    {
        ScoreType.Points => new PointsScoringStrategy(),
        ScoreType.BinaryWin => new BinaryWinScoringStrategy(),
        ScoreType.Objectives => new ObjectivesScoringStrategy(),
        ScoreType.Ranking => new RankingScoringStrategy(),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type,
            $"Unknown ScoreType: {type}"),
    };
}
