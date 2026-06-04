using Api.BoundedContexts.SessionTracking.Domain.Enums;

namespace Api.BoundedContexts.SessionTracking.Domain.Scoring;

/// <summary>
/// DEC-1 stub: Points scoring strategy (filled in T7).
/// Handles numeric points per player (Wingspan, Catan, Azul).
/// </summary>
public sealed class PointsScoringStrategy : IScoringStrategy
{
    public ScoreType Type => ScoreType.Points;

    public ScoringValidationResult Validate(string scoreDataJson) =>
        throw new NotSupportedException("Filled in T7");

    public string Serialize(object scoreData) =>
        throw new NotSupportedException("Filled in T7");

    public object Deserialize(string scoreDataJson) =>
        throw new NotSupportedException("Filled in T7");

    public Guid? ComputeWinnerPlayerId(string scoreDataJson) =>
        throw new NotSupportedException("Filled in T7");
}
