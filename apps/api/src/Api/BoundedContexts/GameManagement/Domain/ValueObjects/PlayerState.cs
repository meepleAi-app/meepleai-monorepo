#pragma warning disable MA0002 // Dictionary without StringComparer
using System.Text.Json.Serialization;

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Value object representing the state of a player in a game session.
/// Issue #2403: Game Session State Tracking.
/// </summary>
internal sealed record PlayerState
{
    /// <summary>
    /// Player name (consistent with SessionPlayer).
    /// </summary>
    public string PlayerName { get; init; }

    /// <summary>
    /// Victory points or score.
    /// </summary>
    public int Score { get; init; }

    /// <summary>
    /// Game-specific resources (e.g., "wood": 3, "stone": 5).
    /// </summary>
    [JsonPropertyName("resources")]
    public IReadOnlyDictionary<string, int> Resources { get; init; }

    /// <summary>
    /// Turn order position (1-based).
    /// </summary>
    public int TurnOrder { get; init; }

    /// <summary>
    /// Whether it's currently this player's turn.
    /// </summary>
    public bool IsCurrentTurn { get; init; }

    /// <summary>
    /// Additional game-specific state (JSON-serializable).
    /// </summary>
    [JsonPropertyName("customState")]
    public IReadOnlyDictionary<string, object>? CustomState { get; init; }

    private PlayerState(
        string playerName,
        int score,
        IReadOnlyDictionary<string, int> resources,
        int turnOrder,
        bool isCurrentTurn,
        IReadOnlyDictionary<string, object>? customState)
    {
        PlayerName = playerName;
        Score = score;
        Resources = resources;
        TurnOrder = turnOrder;
        IsCurrentTurn = isCurrentTurn;
        CustomState = customState;
    }

    /// <summary>
    /// Creates a new player state.
    /// </summary>
    public static PlayerState Create(
        string playerName,
        int score = 0,
        Dictionary<string, int>? resources = null,
        int turnOrder = 1,
        bool isCurrentTurn = false,
        Dictionary<string, object>? customState = null)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            throw new ArgumentException("Player name cannot be empty", nameof(playerName));

        if (playerName.Length > 50)
            throw new ArgumentException("Player name cannot exceed 50 characters", nameof(playerName));

        if (turnOrder < 1)
            throw new ArgumentException("Turn order must be at least 1", nameof(turnOrder));

        return new PlayerState(
            playerName.Trim(),
            score,
            resources ?? new Dictionary<string, int>(),
            turnOrder,
            isCurrentTurn,
            customState);
    }

    /// <summary>
    /// Updates the score.
    /// </summary>
    public PlayerState WithScore(int newScore)
    {
        return this with { Score = newScore };
    }

    /// <summary>
    /// Updates a specific resource value.
    /// </summary>
    public PlayerState WithResource(string resourceName, int value)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
            throw new ArgumentException("Resource name cannot be empty", nameof(resourceName));

        var updated = new Dictionary<string, int>(Resources) { [resourceName] = value };
        return this with { Resources = updated };
    }

    /// <summary>
    /// Sets the current turn flag.
    /// </summary>
    public PlayerState WithCurrentTurn(bool isCurrent)
    {
        return this with { IsCurrentTurn = isCurrent };
    }

    /// <summary>
    /// Updates custom state.
    /// </summary>
    public PlayerState WithCustomState(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Custom state key cannot be empty", nameof(key));

        var updated = CustomState != null
            ? new Dictionary<string, object>(CustomState) { [key] = value }
            : new Dictionary<string, object> { [key] = value };

        return this with { CustomState = updated };
    }
}
