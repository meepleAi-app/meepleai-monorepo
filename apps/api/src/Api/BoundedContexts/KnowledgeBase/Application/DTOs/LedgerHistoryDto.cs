#pragma warning disable MA0002 // Dictionary without StringComparer
namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO for ledger state change history.
/// Issue #2405 - Ledger Mode state tracking
/// </summary>
public sealed record LedgerHistoryDto
{
    /// <summary>
    /// Game session ID
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// List of state change history entries
    /// </summary>
    public List<StateChangeEntry> Changes { get; init; } = new();

    /// <summary>
    /// Current state version
    /// </summary>
    public int CurrentVersion { get; init; }

    /// <summary>
    /// Total number of changes (for pagination)
    /// </summary>
    public int TotalChanges { get; init; }
}

/// <summary>
/// Represents a single state change entry in history
/// </summary>
public sealed record StateChangeEntry
{
    /// <summary>
    /// Timestamp of the change
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// User who made the change
    /// </summary>
    public string UpdatedBy { get; init; } = string.Empty;

    /// <summary>
    /// Description of the change
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// State version after this change
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// Changed properties
    /// </summary>
    public Dictionary<string, object> Changes { get; init; } = new();
}
