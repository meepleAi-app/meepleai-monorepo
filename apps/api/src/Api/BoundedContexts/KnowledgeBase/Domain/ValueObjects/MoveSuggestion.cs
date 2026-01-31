#pragma warning disable MA0002 // Dictionary without StringComparer
namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Represents a suggested move for Player Mode agent behavior.
/// Issue #2404 - Player Mode move suggestions
/// </summary>
public sealed record MoveSuggestion
{
    /// <summary>
    /// The action/move being suggested (e.g., "Costruisci una città", "Trade with port")
    /// </summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// Reasoning and explanation for why this move is recommended
    /// </summary>
    public string Reasoning { get; init; } = string.Empty;

    /// <summary>
    /// Risk level of this move (low, medium, high)
    /// </summary>
    public RiskLevel Risk { get; init; }

    /// <summary>
    /// Confidence score for this suggestion (0.0 to 1.0)
    /// </summary>
    public float ConfidenceScore { get; init; }

    /// <summary>
    /// Expected state changes if this move is applied
    /// </summary>
    public IReadOnlyDictionary<string, object> StateChange { get; init; }
        = new Dictionary<string, object>();

    /// <summary>
    /// Creates a new move suggestion with validation
    /// </summary>
    public static MoveSuggestion Create(
        string action,
        string reasoning,
        RiskLevel risk,
        float confidenceScore,
        Dictionary<string, object>? stateChange = null)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action cannot be empty", nameof(action));

        if (string.IsNullOrWhiteSpace(reasoning))
            throw new ArgumentException("Reasoning cannot be empty", nameof(reasoning));

        if (confidenceScore is < 0.0f or > 1.0f)
            throw new ArgumentOutOfRangeException(
                nameof(confidenceScore),
                "Confidence score must be between 0.0 and 1.0");

        return new MoveSuggestion
        {
            Action = action,
            Reasoning = reasoning,
            Risk = risk,
            ConfidenceScore = confidenceScore,
            StateChange = (stateChange != null)
                ? stateChange.AsReadOnly()
                : (IReadOnlyDictionary<string, object>)new Dictionary<string, object>()
        };
    }
}

/// <summary>
/// Risk level for move suggestions
/// </summary>
public enum RiskLevel
{
    /// <summary>
    /// Low risk - safe move with predictable outcome
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium risk - some uncertainty in outcome
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High risk - significant uncertainty or potential downside
    /// </summary>
    High = 2
}
