#pragma warning disable MA0011 // Use IFormatProvider
namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Represents a conflict between existing game state and newly extracted state.
/// Issue #2405 - Ledger Mode conflict resolution
/// </summary>
public sealed record StateConflict
{
    /// <summary>
    /// The property/field where the conflict occurred (e.g., "score", "roads")
    /// </summary>
    public string PropertyName { get; init; } = string.Empty;

    /// <summary>
    /// The player affected by this conflict (if applicable)
    /// </summary>
    public string? PlayerName { get; init; }

    /// <summary>
    /// Existing value from GameSessionState
    /// </summary>
    public object? ExistingValue { get; init; }

    /// <summary>
    /// New value extracted from message
    /// </summary>
    public object? NewValue { get; init; }

    /// <summary>
    /// Timestamp of the last update to the existing value
    /// </summary>
    public DateTime LastUpdatedAt { get; init; }

    /// <summary>
    /// Message that caused the conflict
    /// </summary>
    public string ConflictingMessage { get; init; } = string.Empty;

    /// <summary>
    /// Severity of the conflict
    /// </summary>
    public ConflictSeverity Severity { get; init; }

    /// <summary>
    /// Suggested resolution strategy
    /// </summary>
    public ConflictResolutionStrategy SuggestedResolution { get; init; }

    /// <summary>
    /// Creates a state conflict with validation
    /// </summary>
    public static StateConflict Create(
        string propertyName,
        string conflictingMessage,
        object? existingValue,
        object? newValue,
        DateTime lastUpdatedAt,
        ConflictSeverity severity,
        string? playerName = null)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            throw new ArgumentException("Property name cannot be empty", nameof(propertyName));

        if (string.IsNullOrWhiteSpace(conflictingMessage))
            throw new ArgumentException("Conflicting message cannot be empty", nameof(conflictingMessage));

        // Determine resolution strategy based on severity and time
        var strategy = DetermineResolutionStrategy(severity, lastUpdatedAt);

        return new StateConflict
        {
            PropertyName = propertyName,
            PlayerName = playerName?.Trim(),
            ExistingValue = existingValue,
            NewValue = newValue,
            LastUpdatedAt = lastUpdatedAt,
            ConflictingMessage = conflictingMessage,
            Severity = severity,
            SuggestedResolution = strategy
        };
    }

    private static ConflictResolutionStrategy DetermineResolutionStrategy(
        ConflictSeverity severity,
        DateTime lastUpdatedAt)
    {
        // If last update was very recent (<30 seconds), likely a mistake
        if ((DateTime.UtcNow - lastUpdatedAt).TotalSeconds < 30)
        {
            return ConflictResolutionStrategy.AskUser;
        }

        return severity switch
        {
            ConflictSeverity.Critical => ConflictResolutionStrategy.AskUser,
            ConflictSeverity.High => ConflictResolutionStrategy.AskUser,
            ConflictSeverity.Medium => ConflictResolutionStrategy.PreferNewest,
            ConflictSeverity.Low => ConflictResolutionStrategy.PreferNewest,
            _ => ConflictResolutionStrategy.AskUser
        };
    }

    /// <summary>
    /// Formats the conflict for display to users
    /// </summary>
    public string FormatForDisplay()
    {
        return $"""
            ⚠️ **Conflitto rilevato:**
            - {(PlayerName != null ? $"Giocatore: {PlayerName}" : "Proprietà")} - {PropertyName}
            - Valore esistente: {FormatValue(ExistingValue)}
            - Nuovo valore: {FormatValue(NewValue)}
            - Ultimo aggiornamento: {FormatTimestamp(LastUpdatedAt)}

            Quale valore è corretto?
            """;
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "non impostato",
            string s => s,
            int i => i.ToString(),
            double d => d.ToString("F2"),
            _ => value.ToString() ?? "sconosciuto"
        };
    }

    private static string FormatTimestamp(DateTime timestamp)
    {
        var elapsed = DateTime.UtcNow - timestamp;
        return elapsed.TotalMinutes < 1
            ? "meno di un minuto fa"
            : elapsed.TotalMinutes < 60
            ? $"{(int)elapsed.TotalMinutes} minuti fa"
            : $"{(int)elapsed.TotalHours} ore fa";
    }
}

/// <summary>
/// Severity levels for state conflicts
/// </summary>
public enum ConflictSeverity
{
    /// <summary>
    /// Low severity - minor discrepancy
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium severity - noticeable difference
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High severity - significant discrepancy
    /// </summary>
    High = 2,

    /// <summary>
    /// Critical severity - major inconsistency requiring immediate resolution
    /// </summary>
    Critical = 3
}

/// <summary>
/// Strategies for resolving state conflicts
/// </summary>
public enum ConflictResolutionStrategy
{
    /// <summary>
    /// Ask the user to choose which value is correct
    /// </summary>
    AskUser = 0,

    /// <summary>
    /// Prefer the newest value (from the conflicting message)
    /// </summary>
    PreferNewest = 1,

    /// <summary>
    /// Keep the existing value (ignore the new one)
    /// </summary>
    KeepExisting = 2,

    /// <summary>
    /// Merge both values (e.g., for incremental changes)
    /// </summary>
    Merge = 3
}
