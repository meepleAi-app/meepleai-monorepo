using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for validating chess move legality (doesn't expose king to check).
/// Issue #3770: Filters illegal moves from pseudo-legal candidates.
/// </summary>
public interface ILegalMoveValidator
{
    /// <summary>
    /// Validates if a candidate move is legal (doesn't leave king in check).
    /// </summary>
    /// <param name="move">Candidate move to validate</param>
    /// <param name="state">Current game state</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if move is legal, false otherwise</returns>
    Task<bool> IsLegalAsync(
        CandidateMove move,
        ParsedGameState state,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a square is under attack by opponent pieces.
    /// </summary>
    bool IsSquareAttacked(
        ChessBoard board,
        ChessPosition square,
        string attackerColor);
}
