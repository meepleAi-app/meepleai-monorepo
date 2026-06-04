using Api.BoundedContexts.SessionTracking.Domain.Enums;

namespace Api.BoundedContexts.SessionTracking.Domain.Scoring;

/// <summary>
/// DEC-1 stub: Binary win/loss scoring strategy (filled in T7).
/// Handles cooperative all-win/all-lose outcomes (Pandemic, Forbidden Island).
/// </summary>
public sealed class BinaryWinScoringStrategy : IScoringStrategy
{
    public ScoreType Type => ScoreType.BinaryWin;

    public ScoringValidationResult Validate(string scoreDataJson) =>
        throw new NotSupportedException("Filled in T7");

    public string Serialize(object scoreData) =>
        throw new NotSupportedException("Filled in T7");

    public object Deserialize(string scoreDataJson) =>
        throw new NotSupportedException("Filled in T7");

    public Guid? ComputeWinnerPlayerId(string scoreDataJson) =>
        throw new NotSupportedException("Filled in T7");
}
