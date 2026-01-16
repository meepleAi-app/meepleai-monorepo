using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Models;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Parses flexible game state dictionary into structured ParsedGameState.
/// Handles various board game schemas gracefully.
/// </summary>
internal sealed class GameStateParser
{
    private readonly ILogger<GameStateParser> _logger;

    public GameStateParser(ILogger<GameStateParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Parses raw game state dictionary into structured format.
    /// Returns null if parsing fails critically (no players found).
    /// </summary>
    public ParsedGameState? Parse(IReadOnlyDictionary<string, object> rawState)
    {
        ArgumentNullException.ThrowIfNull(rawState);

        try
        {
            var players = ParsePlayers(rawState);
            if (players.Count == 0)
            {
                _logger.LogWarning("No players found in game state");
                return null;
            }

            var resources = ParseResources(rawState);
            var board = ParseBoard(rawState);
            var currentPhase = ParseCurrentPhase(rawState);
            var currentTurn = ParseCurrentTurn(rawState);

            var completeness = ParsedGameState.CalculateCompleteness(
                players,
                resources,
                board,
                currentPhase,
                currentTurn
            );

            _logger.LogInformation(
                "Parsed game state: {PlayerCount} players, completeness={Completeness:F2}",
                players.Count,
                completeness
            );

            return new ParsedGameState(
                players,
                resources,
                board,
                currentPhase,
                currentTurn,
                completeness
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse game state");
            return null;
        }
    }

    private IReadOnlyList<PlayerState> ParsePlayers(IReadOnlyDictionary<string, object> state)
    {
        var players = new List<PlayerState>();

        // Try common player keys
        if (state.TryGetValue("players", out var playersObj))
        {
            players.AddRange(ExtractPlayersList(playersObj));
        }
        else if (state.TryGetValue("Players", out var playersObjCap))
        {
            players.AddRange(ExtractPlayersList(playersObjCap));
        }

        // Fallback: look for player1, player2, etc.
        if (players.Count == 0)
        {
            for (var i = 1; i <= 10; i++)
            {
                var key = $"player{i}";
                if (state.TryGetValue(key, out var playerObj))
                {
                    var player = ExtractPlayerState(playerObj, key);
                    if (player != null)
                    {
                        players.Add(player);
                    }
                }
            }
        }

        return players;
    }

    private static IEnumerable<PlayerState> ExtractPlayersList(object playersObj)
    {
        if (playersObj is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            var players = new List<PlayerState>();
            var index = 0;
            foreach (var playerElement in jsonElement.EnumerateArray())
            {
                var player = ExtractPlayerStateFromJsonElement(playerElement, $"player{++index}");
                if (player != null)
                {
                    players.Add(player);
                }
            }
            return players;
        }

        if (playersObj is IEnumerable<object> playersList)
        {
            var players = new List<PlayerState>();
            var index = 0;
            foreach (var playerObj in playersList)
            {
                var player = ExtractPlayerState(playerObj, $"player{++index}");
                if (player != null)
                {
                    players.Add(player);
                }
            }
            return players;
        }

        return Array.Empty<PlayerState>();
    }

    private static PlayerState? ExtractPlayerState(object playerObj, string fallbackName)
    {
        if (playerObj is JsonElement jsonElement)
        {
            return ExtractPlayerStateFromJsonElement(jsonElement, fallbackName);
        }

        if (playerObj is IDictionary<string, object> playerDict)
        {
            var name = playerDict.TryGetValue("name", out var nameObj)
                ? nameObj?.ToString() ?? fallbackName
                : fallbackName;

            var resources = playerDict.TryGetValue("resources", out var resObj)
                ? ExtractDictionary(resObj)
                : null;

            var score = playerDict.TryGetValue("score", out var scoreObj) && int.TryParse(scoreObj?.ToString(), System.Globalization.CultureInfo.InvariantCulture, out var scoreVal)
                ? (int?)scoreVal
                : null;

            var position = playerDict.TryGetValue("position", out var posObj)
                ? posObj?.ToString()
                : null;

            return new PlayerState(name, resources, score, position);
        }

        return null;
    }

    private static PlayerState? ExtractPlayerStateFromJsonElement(JsonElement element, string fallbackName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var name = element.TryGetProperty("name", out var nameProp)
            ? nameProp.GetString() ?? fallbackName
            : fallbackName;

        var resources = element.TryGetProperty("resources", out var resProp)
            ? ExtractDictionary(resProp)
            : null;

        var score = element.TryGetProperty("score", out var scoreProp) && scoreProp.TryGetInt32(out var scoreVal)
            ? (int?)scoreVal
            : null;

        var position = element.TryGetProperty("position", out var posProp)
            ? posProp.GetString()
            : null;

        return new PlayerState(name, resources, score, position);
    }

    private static IReadOnlyDictionary<string, object> ParseResources(IReadOnlyDictionary<string, object> state)
    {
        if (state.TryGetValue("resources", out var resourcesObj))
        {
            return ExtractDictionary(resourcesObj) ?? new Dictionary<string, object>(StringComparer.Ordinal);
        }

        if (state.TryGetValue("Resources", out var resourcesObjCap))
        {
            return ExtractDictionary(resourcesObjCap) ?? new Dictionary<string, object>(StringComparer.Ordinal);
        }

        return new Dictionary<string, object>(StringComparer.Ordinal);
    }

    private static BoardState? ParseBoard(IReadOnlyDictionary<string, object> state)
    {
        if (state.TryGetValue("board", out var boardObj) || state.TryGetValue("Board", out boardObj))
        {
            var boardDict = ExtractDictionary(boardObj);
            if (boardDict != null)
            {
                var description = boardDict.TryGetValue("description", out var descObj)
                    ? descObj?.ToString()
                    : null;

                var tiles = boardDict.TryGetValue("tiles", out var tilesObj)
                    ? ExtractDictionary(tilesObj)
                    : null;

                var pieces = boardDict.TryGetValue("pieces", out var piecesObj)
                    ? ExtractDictionary(piecesObj)
                    : null;

                return new BoardState(description, tiles, pieces);
            }
        }

        return null;
    }

    private static string? ParseCurrentPhase(IReadOnlyDictionary<string, object> state)
    {
        if (state.TryGetValue("currentPhase", out var phaseObj) || state.TryGetValue("phase", out phaseObj))
        {
            return phaseObj?.ToString();
        }

        return null;
    }

    private static int? ParseCurrentTurn(IReadOnlyDictionary<string, object> state)
    {
        if (state.TryGetValue("currentTurn", out var turnObj) || state.TryGetValue("turn", out turnObj))
        {
            if (int.TryParse(turnObj?.ToString(), System.Globalization.CultureInfo.InvariantCulture, out var turnVal))
            {
                return turnVal;
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<string, object>? ExtractDictionary(object obj)
    {
        if (obj is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
        {
            var dict = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var prop in jsonElement.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => prop.Value.TryGetInt32(out var intVal) ? intVal : prop.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => prop.Value
                };
            }
            return dict;
        }

        if (obj is IDictionary<string, object> dictObj)
        {
            return dictObj as IReadOnlyDictionary<string, object> ?? new Dictionary<string, object>(dictObj, StringComparer.Ordinal);
        }

        return null;
    }
}
