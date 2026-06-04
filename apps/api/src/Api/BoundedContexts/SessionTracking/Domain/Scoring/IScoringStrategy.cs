using Api.BoundedContexts.SessionTracking.Domain.Enums;

namespace Api.BoundedContexts.SessionTracking.Domain.Scoring;

/// <summary>
/// DEC-1: Strategy pattern for polymorphic session scoring.
/// Each implementation validates and processes JSON score_data per ScoreType.
/// Concrete strategies: PointsScoringStrategy, BinaryWinScoringStrategy,
/// ObjectivesScoringStrategy, RankingScoringStrategy (T7, T8 implementation).
/// </summary>
public interface IScoringStrategy
{
    /// <summary>The ScoreType this strategy handles.</summary>
    ScoreType Type { get; }

    /// <summary>
    /// Validates a JSON score_data payload against the strategy's schema.
    /// Returns ScoringValidationResult.Valid() or .Failed(errors).
    /// </summary>
    ScoringValidationResult Validate(string scoreDataJson);

    /// <summary>
    /// Serializes a strongly-typed score data object to JSON for persistence.
    /// </summary>
    string Serialize(object scoreData);

    /// <summary>
    /// Deserializes a JSON score_data payload to a strongly-typed object.
    /// </summary>
    object Deserialize(string scoreDataJson);

    /// <summary>
    /// Computes the winner playerId from the score_data. Returns null when
    /// no single winner can be determined (e.g., cooperative all-win/all-lose,
    /// or tied scores).
    /// </summary>
    Guid? ComputeWinnerPlayerId(string scoreDataJson);
}
