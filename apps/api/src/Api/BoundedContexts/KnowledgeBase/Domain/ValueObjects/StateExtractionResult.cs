#pragma warning disable MA0002 // Dictionary without StringComparer

namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Represents the result of extracting game state information from natural language.
/// Issue #2405 - Ledger Mode state tracking
/// </summary>
public sealed record StateExtractionResult
{
    /// <summary>
    /// The type of state change detected
    /// </summary>
    public StateChangeType ChangeType { get; init; }

    /// <summary>
    /// The player affected by this state change (if applicable)
    /// </summary>
    public string? PlayerName { get; init; }

    /// <summary>
    /// Extracted state changes as key-value pairs
    /// Example: {"score": 5, "roads": 3}
    /// </summary>
    public IReadOnlyDictionary<string, object> ExtractedState { get; init; }
        = new Dictionary<string, object>();

    /// <summary>
    /// Confidence score for the extraction (0.0 to 1.0)
    /// </summary>
    public float Confidence { get; init; }

    /// <summary>
    /// The original message that was parsed
    /// </summary>
    public string OriginalMessage { get; init; } = string.Empty;

    /// <summary>
    /// Whether this extraction requires user confirmation before applying
    /// </summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>
    /// Ambiguities or warnings detected during parsing
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Creates a state extraction result with validation
    /// </summary>
    public static StateExtractionResult Create(
        StateChangeType changeType,
        string originalMessage,
        float confidence,
        Dictionary<string, object>? extractedState = null,
        string? playerName = null,
        bool requiresConfirmation = true,
        List<string>? warnings = null)
    {
        if (string.IsNullOrWhiteSpace(originalMessage))
            throw new ArgumentException("Original message cannot be empty", nameof(originalMessage));

        if (confidence is < 0.0f or > 1.0f)
            throw new ArgumentOutOfRangeException(
                nameof(confidence),
                "Confidence must be between 0.0 and 1.0");

        return new StateExtractionResult
        {
            ChangeType = changeType,
            PlayerName = playerName?.Trim(),
            ExtractedState = (extractedState != null)
                ? extractedState.AsReadOnly()
                : (IReadOnlyDictionary<string, object>)new Dictionary<string, object>(),
            Confidence = confidence,
            OriginalMessage = originalMessage,
            RequiresConfirmation = requiresConfirmation,
            Warnings = (warnings != null)
                ? warnings.AsReadOnly()
                : (IReadOnlyList<string>)Array.Empty<string>()
        };
    }

    /// <summary>
    /// Creates a result indicating no state change was detected
    /// </summary>
    public static StateExtractionResult NoChange(string originalMessage)
    {
        return Create(
            changeType: StateChangeType.NoChange,
            originalMessage: originalMessage,
            confidence: 1.0f,
            requiresConfirmation: false);
    }

    /// <summary>
    /// Checks if this result contains valid state changes
    /// </summary>
    public bool HasStateChanges => ChangeType != StateChangeType.NoChange
        && ExtractedState.Count > 0;
}

/// <summary>
/// Types of state changes that can be detected
/// </summary>
public enum StateChangeType
{
    /// <summary>
    /// No state change detected
    /// </summary>
    NoChange = 0,

    /// <summary>
    /// Score/points change (e.g., "ho 5 punti")
    /// </summary>
    ScoreChange = 1,

    /// <summary>
    /// Resource change (e.g., "ho guadagnato 3 legno")
    /// </summary>
    ResourceChange = 2,

    /// <summary>
    /// Player action (e.g., "ho costruito una strada")
    /// </summary>
    PlayerAction = 3,

    /// <summary>
    /// Turn progression (e.g., "tocca a Marco")
    /// </summary>
    TurnChange = 4,

    /// <summary>
    /// Game phase change (e.g., "fase di costruzione")
    /// </summary>
    PhaseChange = 5,

    /// <summary>
    /// Multiple types of changes in one message
    /// </summary>
    Composite = 6
}
