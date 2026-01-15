#pragma warning disable MA0002 // Dictionary without StringComparer
#pragma warning disable MA0009 // Regex DoS vulnerability
#pragma warning disable MA0023 // Add RegexOptions.ExplicitCapture
#pragma warning disable MA0004 // Use ConfigureAwait(false)
#pragma warning disable MA0011 // Use IFormatProvider
#pragma warning disable S1244 // Floating point equality
#pragma warning disable MA0026 // TODO comment
#pragma warning disable S1135 // Complete TODO
using System.Text.Json;
using System.Text.RegularExpressions;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>
/// Natural language parser for extracting game state from Italian chat messages.
/// Uses regex patterns and entity extraction for robust state tracking.
/// Issue #2405 - Ledger Mode NLP parsing
/// </summary>
internal sealed partial class NaturalLanguageStateParser : IStateParser
{
    private readonly ILogger<NaturalLanguageStateParser> _logger;

    // Compiled regex patterns for performance
    private static readonly Regex ScorePattern = ScoreRegex();
    private static readonly Regex ResourcePattern = ResourceRegex();
    private static readonly Regex ActionPattern = ActionRegex();
    private static readonly Regex TurnPattern = TurnRegex();
    private static readonly Regex PhasePattern = PhaseRegex();
    private static readonly Regex PlayerNamePattern = PlayerNameRegex();

    public NaturalLanguageStateParser(ILogger<NaturalLanguageStateParser> logger)
    {
        _logger = logger;
    }

    public async Task<StateExtractionResult> ParseAsync(
        string message,
        JsonDocument? currentState = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return StateExtractionResult.NoChange(message);
        }

        _logger.LogDebug("Parsing message: {Message}", message);

        var extractedState = new Dictionary<string, object>();
        var warnings = new List<string>();
        string? playerName = null;
        StateChangeType changeType = StateChangeType.NoChange;
        float confidence = 0.0f;

        // Extract player name if present
        playerName = ExtractPlayerName(message);

        // Try to extract score changes
        var scoreResult = TryExtractScore(message);
        if (scoreResult.Success)
        {
            extractedState["score"] = scoreResult.Value;
            changeType = StateChangeType.ScoreChange;
            confidence = Math.Max(confidence, scoreResult.Confidence);
        }

        // Try to extract resource changes
        var resourceResult = TryExtractResources(message);
        if (resourceResult.Success)
        {
            foreach (var (key, value) in resourceResult.ExtractedData)
            {
                extractedState[key] = value;
            }
            changeType = changeType == StateChangeType.NoChange
                ? StateChangeType.ResourceChange
                : StateChangeType.Composite;
            confidence = Math.Max(confidence, resourceResult.Confidence);
        }

        // Try to extract player actions
        var actionResult = TryExtractAction(message);
        if (actionResult.Success)
        {
            foreach (var (key, value) in actionResult.ExtractedData)
            {
                extractedState[key] = value;
            }
            changeType = changeType == StateChangeType.NoChange
                ? StateChangeType.PlayerAction
                : StateChangeType.Composite;
            confidence = Math.Max(confidence, actionResult.Confidence);
        }

        // Try to extract turn changes
        var turnResult = TryExtractTurn(message);
        if (turnResult.Success)
        {
            extractedState["currentPlayer"] = turnResult.ExtractedData["currentPlayer"];
            changeType = changeType == StateChangeType.NoChange
                ? StateChangeType.TurnChange
                : StateChangeType.Composite;
            confidence = Math.Max(confidence, turnResult.Confidence);
        }

        // Try to extract phase changes
        var phaseResult = TryExtractPhase(message);
        if (phaseResult.Success)
        {
            extractedState["phase"] = phaseResult.ExtractedData["phase"];
            changeType = changeType == StateChangeType.NoChange
                ? StateChangeType.PhaseChange
                : StateChangeType.Composite;
            confidence = Math.Max(confidence, phaseResult.Confidence);
        }

        // Add warnings if confidence is low
        if (confidence < 0.6f && changeType != StateChangeType.NoChange)
        {
            warnings.Add("Confidenza bassa nell'estrazione dello stato. Verifica la correttezza.");
        }

        // Add warning if multiple changes detected
        if (changeType == StateChangeType.Composite)
        {
            warnings.Add("Rilevate modifiche multiple nello stesso messaggio.");
        }

        // Return result
        var result = StateExtractionResult.Create(
            changeType: changeType,
            originalMessage: message,
            confidence: confidence,
            extractedState: extractedState.Count > 0 ? extractedState : null,
            playerName: playerName,
            requiresConfirmation: confidence < 0.9f || changeType == StateChangeType.Composite,
            warnings: warnings.Count > 0 ? warnings : null);

        _logger.LogDebug(
            "Extraction result: Type={ChangeType}, Confidence={Confidence}, Changes={ChangeCount}",
            result.ChangeType,
            result.Confidence,
            result.ExtractedState.Count);

        return await Task.FromResult(result);
    }

    public async Task<IReadOnlyList<StateConflict>> DetectConflictsAsync(
        StateExtractionResult extractedState,
        JsonDocument currentState,
        DateTime stateLastUpdatedAt,
        CancellationToken cancellationToken = default)
    {
        var conflicts = new List<StateConflict>();

        if (!extractedState.HasStateChanges)
        {
            return conflicts;
        }

        _logger.LogDebug("Detecting conflicts for {ChangeCount} extracted changes", extractedState.ExtractedState.Count);

        // Parse current state JSON
        var currentStateDict = ParseJsonToDictionary(currentState);

        // Compare each extracted property with current state
        foreach (var (key, newValue) in extractedState.ExtractedState)
        {
            if (currentStateDict.TryGetValue(key, out var existingValue))
            {
                // Check if values conflict
                if (!ValuesAreEqual(existingValue, newValue))
                {
                    var severity = DetermineConflictSeverity(key, existingValue, newValue);

                    var conflict = StateConflict.Create(
                        propertyName: key,
                        conflictingMessage: extractedState.OriginalMessage,
                        existingValue: existingValue,
                        newValue: newValue,
                        lastUpdatedAt: stateLastUpdatedAt,
                        severity: severity,
                        playerName: extractedState.PlayerName);

                    conflicts.Add(conflict);

                    _logger.LogWarning(
                        "Conflict detected: {PropertyName} - Existing={ExistingValue}, New={NewValue}, Severity={Severity}",
                        key,
                        existingValue,
                        newValue,
                        severity);
                }
            }
        }

        return await Task.FromResult(conflicts);
    }

    public async Task<JsonDocument> GenerateStatePatchAsync(
        StateExtractionResult extraction,
        JsonDocument currentState,
        CancellationToken cancellationToken = default)
    {
        if (!extraction.HasStateChanges)
        {
            return currentState;
        }

        _logger.LogDebug("Generating state patch for {ChangeCount} changes", extraction.ExtractedState.Count);

        // Parse current state to dictionary
        var stateDict = ParseJsonToDictionary(currentState);

        // Apply extracted changes
        foreach (var (key, value) in extraction.ExtractedState)
        {
            stateDict[key] = value;
        }

        // Convert back to JsonDocument
        var json = JsonSerializer.Serialize(stateDict);
        var newState = JsonDocument.Parse(json);

        return await Task.FromResult(newState);
    }

    #region Pattern Extraction Methods

    /// <summary>
    /// Extracts player name from message
    /// </summary>
    private static string? ExtractPlayerName(string message)
    {
        var match = PlayerNamePattern.Match(message);
        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value.Trim();
        }
        return null;
    }

    /// <summary>
    /// Tries to extract score/points from message
    /// </summary>
    private ExtractionResult TryExtractScore(string message)
    {
        var match = ScorePattern.Match(message);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var score))
        {
            _logger.LogDebug("Extracted score: {Score}", score);
            return new ExtractionResult
            {
                Success = true,
                Value = score,
                Confidence = 0.9f
            };
        }
        return ExtractionResult.Failed();
    }

    /// <summary>
    /// Tries to extract resource changes from message
    /// </summary>
    private ExtractionResult TryExtractResources(string message)
    {
        var matches = ResourcePattern.Matches(message);
        if (matches.Count == 0)
        {
            return ExtractionResult.Failed();
        }

        var extractedData = new Dictionary<string, object>();
        var maxConfidence = 0.0f;

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 2 &&
                int.TryParse(match.Groups[1].Value, out var quantity))
            {
                var resource = NormalizeResourceName(match.Groups[2].Value);
                extractedData[$"resources.{resource}"] = quantity;
                maxConfidence = Math.Max(maxConfidence, 0.85f);

                _logger.LogDebug("Extracted resource: {Resource}={Quantity}", resource, quantity);
            }
        }

        return extractedData.Count > 0
            ? new ExtractionResult { Success = true, ExtractedData = extractedData, Confidence = maxConfidence }
            : ExtractionResult.Failed();
    }

    /// <summary>
    /// Tries to extract player actions from message
    /// </summary>
    private ExtractionResult TryExtractAction(string message)
    {
        var match = ActionPattern.Match(message);
        if (match.Success && match.Groups.Count > 2)
        {
            var action = match.Groups[1].Value.ToLowerInvariant();
            var target = match.Groups[2].Value;

            var extractedData = new Dictionary<string, object>();

            // Map actions to state properties
            switch (action)
            {
                case "costruito" or "build":
                    var buildingType = NormalizeBuildingType(target);
                    extractedData[$"buildings.{buildingType}"] = 1; // Increment by 1
                    break;
                case "comprato" or "bought":
                    extractedData["cards"] = 1; // Increment cards
                    break;
            }

            if (extractedData.Count > 0)
            {
                _logger.LogDebug("Extracted action: {Action} {Target}", action, target);
                return new ExtractionResult
                {
                    Success = true,
                    ExtractedData = extractedData,
                    Confidence = 0.8f
                };
            }
        }

        return ExtractionResult.Failed();
    }

    /// <summary>
    /// Tries to extract turn changes from message
    /// </summary>
    private ExtractionResult TryExtractTurn(string message)
    {
        var match = TurnPattern.Match(message);
        if (match.Success && match.Groups.Count > 1)
        {
            var playerName = match.Groups[1].Value.Trim();
            _logger.LogDebug("Extracted turn change: {PlayerName}", playerName);

            return new ExtractionResult
            {
                Success = true,
                ExtractedData = new Dictionary<string, object>
                {
                    { "currentPlayer", playerName }
                },
                Confidence = 0.95f
            };
        }
        return ExtractionResult.Failed();
    }

    /// <summary>
    /// Tries to extract phase changes from message
    /// </summary>
    private ExtractionResult TryExtractPhase(string message)
    {
        var match = PhasePattern.Match(message);
        if (match.Success && match.Groups.Count > 1)
        {
            var phase = match.Groups[1].Value.Trim();
            _logger.LogDebug("Extracted phase change: {Phase}", phase);

            return new ExtractionResult
            {
                Success = true,
                ExtractedData = new Dictionary<string, object>
                {
                    { "phase", phase }
                },
                Confidence = 0.9f
            };
        }
        return ExtractionResult.Failed();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Parses JsonDocument to Dictionary for easier manipulation
    /// </summary>
    private static Dictionary<string, object?> ParseJsonToDictionary(JsonDocument jsonDoc)
    {
        var dict = new Dictionary<string, object?>();

        if (jsonDoc.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in jsonDoc.RootElement.EnumerateObject())
            {
                dict[property.Name] = ParseJsonValue(property.Value);
            }
        }

        return dict;
    }

    /// <summary>
    /// Parses JsonElement to object
    /// </summary>
    private static object? ParseJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var i) ? i : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    /// <summary>
    /// Checks if two values are equal (handles different types)
    /// </summary>
    private static bool ValuesAreEqual(object? value1, object? value2)
    {
        if (value1 == null && value2 == null) return true;
        if (value1 == null || value2 == null) return false;

        // Handle numeric comparisons with epsilon tolerance for floating point
        if (IsNumeric(value1) && IsNumeric(value2))
        {
            const double epsilon = 0.0001;
            return Math.Abs(Convert.ToDouble(value1) - Convert.ToDouble(value2)) < epsilon;
        }

        return value1.Equals(value2);
    }

    /// <summary>
    /// Checks if value is numeric
    /// </summary>
    private static bool IsNumeric(object value)
    {
        return value is int or long or float or double or decimal;
    }

    /// <summary>
    /// Determines conflict severity based on property and value difference
    /// </summary>
    private static ConflictSeverity DetermineConflictSeverity(
        string propertyName,
        object? existingValue,
        object? newValue)
    {
        // Score conflicts are typically critical
        if (propertyName.Equals("score", StringComparison.OrdinalIgnoreCase))
        {
            return ConflictSeverity.Critical;
        }

        // Large numeric differences are high severity
        if (IsNumeric(existingValue) && IsNumeric(newValue))
        {
            var diff = Math.Abs(Convert.ToDouble(newValue) - Convert.ToDouble(existingValue));
            return diff switch
            {
                > 10 => ConflictSeverity.High,
                > 5 => ConflictSeverity.Medium,
                _ => ConflictSeverity.Low
            };
        }

        // Default to medium severity
        return ConflictSeverity.Medium;
    }

    /// <summary>
    /// Normalizes resource names to standard keys
    /// </summary>
    private static string NormalizeResourceName(string resource)
    {
        return resource.ToLowerInvariant() switch
        {
            "legno" or "wood" => "wood",
            "pietra" or "stone" => "stone",
            "grano" or "wheat" => "wheat",
            "argilla" or "clay" => "clay",
            "pecora" or "sheep" => "sheep",
            _ => resource.ToLowerInvariant()
        };
    }

    /// <summary>
    /// Normalizes building types to standard keys
    /// </summary>
    private static string NormalizeBuildingType(string building)
    {
        return building.ToLowerInvariant() switch
        {
            "strada" or "road" => "roads",
            "insediamento" or "settlement" => "settlements",
            "città" or "city" => "cities",
            _ => building.ToLowerInvariant()
        };
    }

    #endregion

    #region Regex Patterns (Source Generated)

    /// <summary>
    /// Matches score/points patterns in Italian
    /// Examples: "ho 5 punti", "sono a 10", "passo da 3 a 5 punti"
    /// </summary>
    [GeneratedRegex(@"(?:ho|sono a|passo (?:da \d{1,5} )?a)\s{0,10}(\d{1,5})\s{0,10}(?:punti?|pt)?", RegexOptions.IgnoreCase | RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ScoreRegex();

    /// <summary>
    /// Matches resource patterns in Italian
    /// Examples: "ho 3 legno", "guadagnato 2 pietra", "perso 1 grano"
    /// </summary>
    [GeneratedRegex(@"(?:ho|guadagnato|perso|gained|lost)\s{0,10}(\d{1,5})\s{0,10}(legno|pietra|grano|argilla|pecora|wood|stone|wheat|clay|sheep)", RegexOptions.IgnoreCase | RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ResourceRegex();

    /// <summary>
    /// Matches player action patterns in Italian
    /// Examples: "ho costruito una strada", "ho comprato una carta"
    /// </summary>
    [GeneratedRegex(@"ho\s{1,10}(costruito|comprato|build|bought)\s{1,10}(?:una?\s{1,5})?(strada|insediamento|città|carta|road|settlement|city|card)", RegexOptions.IgnoreCase | RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ActionRegex();

    /// <summary>
    /// Matches turn change patterns in Italian
    /// Examples: "tocca a Marco", "è il turno di Luca"
    /// </summary>
    [GeneratedRegex(@"(?:tocca a|è il turno di|turn of)\s{1,10}([A-Z][a-z]{1,30})", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TurnRegex();

    /// <summary>
    /// Matches phase change patterns in Italian
    /// Examples: "fase di costruzione", "inizia il round 3"
    /// </summary>
    [GeneratedRegex(@"(?:fase di|inizia il round|phase)\s{1,10}([a-zA-Z0-9\s]{1,50})", RegexOptions.IgnoreCase | RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PhaseRegex();

    /// <summary>
    /// Matches player names (capitalized words)
    /// Examples: "Marco ha", "Luca gioca"
    /// </summary>
    [GeneratedRegex(@"^([A-Z][a-z]{1,30})\s{1,10}(?:ha|gioca|plays|è)", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PlayerNameRegex();

    #endregion

    /// <summary>
    /// Internal result struct for extraction operations
    /// </summary>
    private sealed class ExtractionResult
    {
        public bool Success { get; init; }
        public object Value { get; init; } = new();
        public Dictionary<string, object> ExtractedData { get; init; } = new();
        public float Confidence { get; init; }

        public static ExtractionResult Failed() => new() { Success = false, Confidence = 0.0f };
    }
}
