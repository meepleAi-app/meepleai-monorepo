#pragma warning disable MA0002 // Dictionary without StringComparer
namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO for ledger message parsing results.
/// Issue #2405 - Ledger Mode state tracking
/// </summary>
public sealed record LedgerParseResultDto
{
    /// <summary>
    /// Type of state change detected
    /// </summary>
    public string ChangeType { get; init; } = "NoChange";

    /// <summary>
    /// Player name extracted from message (if any)
    /// </summary>
    public string? PlayerName { get; init; }

    /// <summary>
    /// Extracted state changes as key-value pairs
    /// </summary>
    public Dictionary<string, object> ExtractedState { get; init; } = new();

    /// <summary>
    /// Confidence score (0.0 to 1.0)
    /// </summary>
    public float Confidence { get; init; }

    /// <summary>
    /// Original message that was parsed
    /// </summary>
    public string OriginalMessage { get; init; } = string.Empty;

    /// <summary>
    /// Whether this change requires user confirmation
    /// </summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>
    /// Warnings or ambiguities detected
    /// </summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>
    /// Conflicts detected with current state
    /// </summary>
    public List<StateConflictDto> Conflicts { get; init; } = new();
}

/// <summary>
/// DTO for state conflicts
/// </summary>
public sealed record StateConflictDto
{
    /// <summary>
    /// Property where conflict occurred
    /// </summary>
    public string PropertyName { get; init; } = string.Empty;

    /// <summary>
    /// Player affected (if any)
    /// </summary>
    public string? PlayerName { get; init; }

    /// <summary>
    /// Existing value in state
    /// </summary>
    public object? ExistingValue { get; init; }

    /// <summary>
    /// New value from message
    /// </summary>
    public object? NewValue { get; init; }

    /// <summary>
    /// Severity level (Low, Medium, High, Critical)
    /// </summary>
    public string Severity { get; init; } = "Medium";

    /// <summary>
    /// Suggested resolution strategy
    /// </summary>
    public string SuggestedResolution { get; init; } = "AskUser";

    /// <summary>
    /// User-friendly formatted conflict message
    /// </summary>
    public string FormattedMessage { get; init; } = string.Empty;
}
