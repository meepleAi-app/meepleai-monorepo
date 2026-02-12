using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for generating candidate chess moves with priority ranking.
/// Issue #3770: Core interface for Decisore Agent move suggestion.
/// </summary>
public interface IMoveGeneratorService
{
    /// <summary>
    /// Generates legal candidate moves for a player with heuristic scoring and priority ranking.
    /// </summary>
    /// <param name="state">Current parsed game state</param>
    /// <param name="playerColor">Player color ("White" or "Black")</param>
    /// <param name="maxCandidates">Maximum number of candidates to return (default: 10)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of candidate moves ranked by priority and score</returns>
    Task<List<CandidateMove>> GenerateCandidatesAsync(
        ParsedGameState state,
        string playerColor,
        int maxCandidates = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a specific move is legal in the current state.
    /// </summary>
    Task<bool> IsLegalMoveAsync(
        ParsedGameState state,
        ChessPosition from,
        ChessPosition toPosition,
        CancellationToken cancellationToken = default);
}
