using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Factory for creating game-specific state parsers.
/// Issue #3772: Enables multi-game support with extensible parser selection.
/// </summary>
public sealed class GameStateParserFactory : IGameStateParserService
{
    private readonly ChessFENParser _chessParser;

    public GameStateParserFactory()
    {
        _chessParser = new ChessFENParser();
    }

    /// <inheritdoc/>
    public Task<ParsedGameState> ParseAsync(
        string rawState,
        string gameTitle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawState);
        ArgumentNullException.ThrowIfNull(gameTitle);

        var parser = GetParser(gameTitle);
        var result = parser.Parse(rawState);

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public bool SupportsGame(string gameTitle)
    {
        try
        {
            _ = GetParser(gameTitle);
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetSupportedGames()
    {
        return new List<string> { "Chess" };
    }

    private ChessFENParser GetParser(string gameTitle)
    {
        var normalizedTitle = gameTitle.Trim();

        // Case-insensitive matching for common chess variants
        if (normalizedTitle.Equals("Chess", StringComparison.OrdinalIgnoreCase) ||
            normalizedTitle.Equals("Chess960", StringComparison.OrdinalIgnoreCase) ||
            normalizedTitle.Contains("Chess", StringComparison.OrdinalIgnoreCase))
        {
            return _chessParser;
        }

        throw new NotSupportedException($"Game type '{gameTitle}' is not supported. Supported games: {string.Join(", ", GetSupportedGames())}");
    }
}
