using Api.BoundedContexts.SessionTracking.Domain.Enums;

namespace Api.BoundedContexts.SessionTracking.Domain.Scoring;

/// <summary>
/// DEC-1 stub: Objectives-based scoring strategy (filled in T8).
/// Handles per-player objective completion counts (T.I.M.E. Stories, Sherlock Holmes).
/// </summary>
public sealed class ObjectivesScoringStrategy : IScoringStrategy
{
    public ScoreType Type => ScoreType.Objectives;

    public ScoringValidationResult Validate(string scoreDataJson) =>
        throw new NotSupportedException("Filled in T8");

    public string Serialize(object scoreData) =>
        throw new NotSupportedException("Filled in T8");

    public object Deserialize(string scoreDataJson) =>
        throw new NotSupportedException("Filled in T8");

    public Guid? ComputeWinnerPlayerId(string scoreDataJson) =>
        throw new NotSupportedException("Filled in T8");
}
