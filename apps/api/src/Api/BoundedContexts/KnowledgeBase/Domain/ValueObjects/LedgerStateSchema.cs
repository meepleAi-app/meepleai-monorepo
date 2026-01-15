#pragma warning disable MA0002 // Dictionary without StringComparer
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Schema for ledger mode structured state tracking.
/// Defines the format for GameSessionState JsonDocument when used with Ledger Mode.
/// Issue #2405: Ledger Mode - Full State Tracking.
/// </summary>
internal sealed record LedgerStateSchema
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions DeserializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    /// <summary>
    /// Current game phase.
    /// </summary>
    [JsonPropertyName("currentPhase")]
    public string CurrentPhase { get; init; } = GamePhase.Setup.ToString();

    /// <summary>
    /// Current round number (1-based).
    /// </summary>
    [JsonPropertyName("currentRound")]
    public int CurrentRound { get; init; } = 1;

    /// <summary>
    /// Player states keyed by player name.
    /// </summary>
    [JsonPropertyName("players")]
    public Dictionary<string, LedgerPlayerState> Players { get; init; } = new();

    /// <summary>
    /// State change history.
    /// </summary>
    [JsonPropertyName("history")]
    public List<LedgerStateChange> History { get; init; } = new();

    /// <summary>
    /// Custom game-specific state (optional).
    /// </summary>
    [JsonPropertyName("customState")]
    public Dictionary<string, object>? CustomState { get; init; }

    /// <summary>
    /// Creates an empty ledger state.
    /// </summary>
    public static LedgerStateSchema CreateEmpty(IEnumerable<string> playerNames)
    {
        ArgumentNullException.ThrowIfNull(playerNames);

        var schema = new LedgerStateSchema
        {
            Players = new Dictionary<string, LedgerPlayerState>()
        };

        int turnOrder = 1;
        foreach (var playerName in playerNames)
        {
            schema.Players[playerName] = new LedgerPlayerState
            {
                PlayerName = playerName,
                Score = 0,
                Resources = new Dictionary<string, int>(),
                TurnOrder = turnOrder,
                IsCurrentTurn = turnOrder == 1
            };
            turnOrder++;
        }

        return schema;
    }

    /// <summary>
    /// Serializes to JsonDocument.
    /// </summary>
    public JsonDocument ToJsonDocument()
    {
        var json = JsonSerializer.Serialize(this, SerializerOptions);
        return JsonDocument.Parse(json);
    }

    /// <summary>
    /// Deserializes from JsonDocument.
    /// </summary>
    public static LedgerStateSchema FromJsonDocument(JsonDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var json = document.RootElement.GetRawText();
        return JsonSerializer.Deserialize<LedgerStateSchema>(json, DeserializerOptions)
            ?? throw new InvalidOperationException("Failed to deserialize LedgerStateSchema");
    }
}

/// <summary>
/// Player state within ledger schema.
/// </summary>
internal sealed record LedgerPlayerState
{
    [JsonPropertyName("playerName")]
    public string PlayerName { get; init; } = string.Empty;

    [JsonPropertyName("score")]
    public int Score { get; init; }

    [JsonPropertyName("resources")]
    public Dictionary<string, int> Resources { get; init; } = new();

    [JsonPropertyName("turnOrder")]
    public int TurnOrder { get; init; } = 1;

    [JsonPropertyName("isCurrentTurn")]
    public bool IsCurrentTurn { get; init; }

    [JsonPropertyName("customState")]
    public Dictionary<string, object>? CustomState { get; init; }
}

/// <summary>
/// State change record within ledger schema.
/// </summary>
internal sealed record LedgerStateChange
{
    [JsonPropertyName("playerName")]
    public string? PlayerName { get; init; }

    [JsonPropertyName("changeType")]
    public string ChangeType { get; init; } = string.Empty;

    [JsonPropertyName("fieldName")]
    public string FieldName { get; init; } = string.Empty;

    [JsonPropertyName("oldValue")]
    public string? OldValue { get; init; }

    [JsonPropertyName("newValue")]
    public string NewValue { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("source")]
    public string Source { get; init; } = "ledger-agent";

    [JsonPropertyName("isConfirmed")]
    public bool IsConfirmed { get; init; }
}
