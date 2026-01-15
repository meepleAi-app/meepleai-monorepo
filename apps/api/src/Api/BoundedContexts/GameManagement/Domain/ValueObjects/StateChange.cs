namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Value object representing a tracked state change in a game session.
/// Issue #2403: Game Session State Tracking.
/// </summary>
internal sealed record StateChange
{
    /// <summary>
    /// Player who initiated the change (if applicable).
    /// </summary>
    public string? PlayerName { get; init; }

    /// <summary>
    /// Type of change (score, resource, turn, phase).
    /// </summary>
    public StateChangeType ChangeType { get; init; }

    /// <summary>
    /// Field or property that changed (e.g., "score", "wood", "currentTurn").
    /// </summary>
    public string FieldName { get; init; }

    /// <summary>
    /// Previous value (null for new fields).
    /// </summary>
    public string? OldValue { get; init; }

    /// <summary>
    /// New value.
    /// </summary>
    public string NewValue { get; init; }

    /// <summary>
    /// When the change occurred.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Source of the change (manual, ledger-agent, api).
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Whether this change was confirmed by the user.
    /// </summary>
    public bool IsConfirmed { get; init; }

    private StateChange(
        string? playerName,
        StateChangeType changeType,
        string fieldName,
        string? oldValue,
        string newValue,
        DateTime timestamp,
        string source,
        bool isConfirmed)
    {
        PlayerName = playerName;
        ChangeType = changeType;
        FieldName = fieldName;
        OldValue = oldValue;
        NewValue = newValue;
        Timestamp = timestamp;
        Source = source;
        IsConfirmed = isConfirmed;
    }

    /// <summary>
    /// Creates a new state change record.
    /// </summary>
    public static StateChange Create(
        StateChangeType changeType,
        string fieldName,
        string newValue,
        string? oldValue = null,
        string? playerName = null,
        string source = "ledger-agent",
        bool isConfirmed = false)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("Field name cannot be empty", nameof(fieldName));

        if (string.IsNullOrWhiteSpace(newValue))
            throw new ArgumentException("New value cannot be empty", nameof(newValue));

        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source cannot be empty", nameof(source));

        return new StateChange(
            playerName?.Trim(),
            changeType,
            fieldName.Trim(),
            oldValue?.Trim(),
            newValue.Trim(),
            DateTime.UtcNow,
            source.Trim(),
            isConfirmed);
    }

    /// <summary>
    /// Marks this change as confirmed.
    /// </summary>
    public StateChange Confirm()
    {
        return this with { IsConfirmed = true };
    }
}

/// <summary>
/// Type of state change.
/// </summary>
internal enum StateChangeType
{
    /// <summary>
    /// Score or victory point change.
    /// </summary>
    Score = 0,

    /// <summary>
    /// Resource change (wood, stone, etc.).
    /// </summary>
    Resource = 1,

    /// <summary>
    /// Turn progression.
    /// </summary>
    Turn = 2,

    /// <summary>
    /// Game phase change.
    /// </summary>
    Phase = 3,

    /// <summary>
    /// Custom state change.
    /// </summary>
    Custom = 4
}
