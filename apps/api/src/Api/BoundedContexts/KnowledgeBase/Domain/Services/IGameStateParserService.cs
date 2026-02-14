using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for parsing raw game state into structured representation.
/// Issue #3772: Enables Decisore Agent strategic analysis across multiple game types.
/// </summary>
public interface IGameStateParserService
{
    /// <summary>
    /// Parses raw board state string into structured ParsedGameState.
    /// </summary>
    /// <param name="rawState">Raw state string (e.g., FEN notation for chess)</param>
    /// <param name="gameTitle">Game title for parser selection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Structured game state ready for analysis</returns>
    /// <exception cref="Exceptions.InvalidGameStateException">Thrown when state is malformed or invalid</exception>
    /// <exception cref="NotSupportedException">Thrown when game type is not supported</exception>
    Task<ParsedGameState> ParseAsync(
        string rawState,
        string gameTitle,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a game type is supported by the parser.
    /// </summary>
    bool SupportsGame(string gameTitle);

    /// <summary>
    /// Gets list of supported game types.
    /// </summary>
    IReadOnlyList<string> GetSupportedGames();
}
