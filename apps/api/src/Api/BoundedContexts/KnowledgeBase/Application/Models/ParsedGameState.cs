namespace Api.BoundedContexts.KnowledgeBase.Application.Models;

/// <summary>
/// Parsed and structured game state for AI analysis.
/// Supports flexible board game schemas (Catan, Ticket to Ride, etc.).
/// </summary>
/// <param name="Players">List of players with their state (min 1 required)</param>
/// <param name="Resources">Available resources dictionary</param>
/// <param name="Board">Optional board state representation</param>
/// <param name="CurrentPhase">Optional current game phase (e.g., "setup", "main", "endgame")</param>
/// <param name="CurrentTurn">Optional turn number</param>
/// <param name="CompletenessScore">Score indicating how complete the game state is (0.0-1.0)</param>
internal sealed record ParsedGameState(
    IReadOnlyList<PlayerState> Players,
    IReadOnlyDictionary<string, object> Resources,
    BoardState? Board,
    string? CurrentPhase,
    int? CurrentTurn,
    double CompletenessScore
)
{
    /// <summary>
    /// Calculates completeness score based on available data.
    /// </summary>
    public static double CalculateCompleteness(
        IReadOnlyList<PlayerState> players,
        IReadOnlyDictionary<string, object> resources,
        BoardState? board,
        string? currentPhase,
        int? currentTurn)
    {
        var score = 0.0;

        // Players (required): 40%
        if (players.Count > 0)
        {
            score += 0.4;
        }

        // Resources: 20%
        if (resources.Count > 0)
        {
            score += 0.2;
        }

        // Board state: 20%
        if (board != null)
        {
            score += 0.2;
        }

        // Phase: 10%
        if (!string.IsNullOrWhiteSpace(currentPhase))
        {
            score += 0.1;
        }

        // Turn number: 10%
        if (currentTurn.HasValue && currentTurn.Value > 0)
        {
            score += 0.1;
        }

        return score;
    }

    /// <summary>
    /// Generates a summary string for LLM prompts.
    /// </summary>
    public string ToSummary()
    {
        var parts = new List<string>
        {
            $"Players: {Players.Count}",
            $"Resources: {string.Join(", ", Resources.Select(kv => $"{kv.Key}={kv.Value}"))}"
        };

        if (!string.IsNullOrWhiteSpace(CurrentPhase))
        {
            parts.Add($"Phase: {CurrentPhase}");
        }

        if (CurrentTurn.HasValue)
        {
            parts.Add($"Turn: {CurrentTurn}");
        }

        if (Board != null)
        {
            parts.Add($"Board: {Board.Description ?? "Present"}");
        }

        return string.Join(" | ", parts);
    }
}

/// <summary>
/// Player state information.
/// </summary>
/// <param name="Name">Player name or identifier</param>
/// <param name="Resources">Player's resources</param>
/// <param name="Score">Optional player score</param>
/// <param name="Position">Optional board position</param>
internal sealed record PlayerState(
    string Name,
    IReadOnlyDictionary<string, object>? Resources,
    int? Score,
    string? Position
);

/// <summary>
/// Board state representation.
/// </summary>
/// <param name="Description">Textual description of board state</param>
/// <param name="Tiles">Optional tile/hex configuration</param>
/// <param name="Pieces">Optional piece positions</param>
internal sealed record BoardState(
    string? Description,
    IReadOnlyDictionary<string, object>? Tiles,
    IReadOnlyDictionary<string, object>? Pieces
);
