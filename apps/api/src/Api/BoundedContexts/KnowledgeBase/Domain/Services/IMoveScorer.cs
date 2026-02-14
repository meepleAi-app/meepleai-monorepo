using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for heuristic move scoring (material, positional, tactical, development).
/// Issue #3770: Provides rapid evaluation without LLM for candidate filtering.
/// </summary>
public interface IMoveScorer
{
    /// <summary>
    /// Calculates heuristic score for a candidate move.
    /// </summary>
    /// <param name="move">Move to score (with from/to/capture info)</param>
    /// <param name="state">Current game state for context</param>
    /// <param name="playerColor">Player making the move</param>
    /// <returns>MoveScore with component breakdown</returns>
    MoveScore Score(
        CandidateMove move,
        ParsedGameState state,
        string playerColor);
}
