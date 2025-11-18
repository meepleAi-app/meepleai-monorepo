namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Represents a player move in a board game session.
/// Immutable value object containing move details for validation.
/// </summary>
public sealed record Move
{
    /// <summary>
    /// Name of the player making the move.
    /// </summary>
    public string PlayerName { get; init; }

    /// <summary>
    /// Description of the action being performed (e.g., "roll dice", "move piece", "draw card").
    /// </summary>
    public string Action { get; init; }

    /// <summary>
    /// Optional position or coordinates (e.g., "A5", "3,4", "red space").
    /// </summary>
    public string? Position { get; init; }

    /// <summary>
    /// When the move was made.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Optional additional context for complex moves (e.g., card name, resource type).
    /// </summary>
    public IReadOnlyDictionary<string, string>? AdditionalContext { get; init; }

    /// <summary>
    /// Creates a new Move value object.
    /// </summary>
    public Move(
        string playerName,
        string action,
        string? position = null,
        DateTime? timestamp = null,
        IReadOnlyDictionary<string, string>? additionalContext = null)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            throw new ArgumentException("Player name cannot be empty", nameof(playerName));

        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action cannot be empty", nameof(action));

        PlayerName = playerName.Trim();
        Action = action.Trim();
        Position = string.IsNullOrWhiteSpace(position) ? null : position.Trim();
        Timestamp = timestamp ?? DateTime.UtcNow;
        AdditionalContext = additionalContext;
    }

    /// <summary>
    /// Returns a string representation of the move.
    /// </summary>
    public override string ToString()
    {
        var positionPart = Position != null ? $" at {Position}" : string.Empty;
        return $"{PlayerName}: {Action}{positionPart}";
    }
}
