using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value object representing the current state of a game session for agent context.
/// </summary>
/// <remarks>
/// Immutable JSON-backed state with schema validation.
/// Schema: { currentTurn, activePlayer, playerScores, gamePhase, lastAction }
/// </remarks>
internal sealed record GameState
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions s_jsonWriteOptions = new()
    {
        WriteIndented = false
    };

    public int CurrentTurn { get; init; }
    public Guid ActivePlayer { get; init; }
    public IReadOnlyDictionary<Guid, decimal> PlayerScores { get; init; }
    public string GamePhase { get; init; }
    public string LastAction { get; init; }

    [JsonConstructor]
    private GameState(
        int currentTurn,
        Guid activePlayer,
        IReadOnlyDictionary<Guid, decimal> playerScores,
        string gamePhase,
        string lastAction)
    {
        CurrentTurn = currentTurn;
        ActivePlayer = activePlayer;
        PlayerScores = playerScores ?? new Dictionary<Guid, decimal>();
        GamePhase = gamePhase ?? string.Empty;
        LastAction = lastAction ?? string.Empty;
    }

    /// <summary>
    /// Creates a new game state with validation.
    /// </summary>
    public static GameState Create(
        int currentTurn,
        Guid activePlayer,
        IDictionary<Guid, decimal> playerScores,
        string gamePhase,
        string lastAction)
    {
        if (currentTurn < 0)
            throw new ArgumentException("CurrentTurn cannot be negative", nameof(currentTurn));
        if (activePlayer == Guid.Empty)
            throw new ArgumentException("ActivePlayer cannot be empty", nameof(activePlayer));
        if (string.IsNullOrWhiteSpace(gamePhase))
            throw new ArgumentException("GamePhase cannot be empty", nameof(gamePhase));
        if (string.IsNullOrWhiteSpace(lastAction))
            throw new ArgumentException("LastAction cannot be empty", nameof(lastAction));

        ArgumentNullException.ThrowIfNull(playerScores);

        if (gamePhase.Length > 50)
            throw new ArgumentException("GamePhase cannot exceed 50 characters", nameof(gamePhase));
        if (lastAction.Length > 200)
            throw new ArgumentException("LastAction cannot exceed 200 characters", nameof(lastAction));

        return new GameState(
            currentTurn,
            activePlayer,
            playerScores is Dictionary<Guid, decimal> dict ? dict : new Dictionary<Guid, decimal>(playerScores),
            gamePhase.Trim(),
            lastAction.Trim());
    }

    /// <summary>
    /// Parses JSON string into GameState with validation.
    /// </summary>
    public static GameState FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON cannot be empty", nameof(json));

        try
        {
            var state = JsonSerializer.Deserialize<GameState>(json, s_jsonOptions);

            if (state == null)
                throw new ArgumentException("Invalid JSON: deserialization returned null", nameof(json));

            // Re-validate through factory to ensure all constraints
            return Create(
                state.CurrentTurn,
                state.ActivePlayer,
                state.PlayerScores as IDictionary<Guid, decimal> ?? new Dictionary<Guid, decimal>(),
                state.GamePhase,
                state.LastAction);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid GameState JSON: {ex.Message}", nameof(json), ex);
        }
    }

    /// <summary>
    /// Serializes GameState to JSON string.
    /// </summary>
    public string ToJson()
        => JsonSerializer.Serialize(this, s_jsonWriteOptions);

    /// <summary>
    /// Creates an initial empty state for a new session.
    /// </summary>
    public static GameState Initial(Guid activePlayer)
        => Create(
            currentTurn: 1,
            activePlayer: activePlayer,
            playerScores: new Dictionary<Guid, decimal>(),
            gamePhase: "setup",
            lastAction: "session started");
}