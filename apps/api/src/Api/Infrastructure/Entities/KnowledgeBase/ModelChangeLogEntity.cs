namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// Database entity tracking model changes (swaps, deprecations, availability changes).
/// Provides audit trail for E5 admin UI change history.
/// Issue #5496: Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
public class ModelChangeLogEntity
{
    /// <summary>
    /// Unique identifier for the change log entry.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The model ID affected by the change.
    /// </summary>
    public string ModelId { get; set; } = default!;

    /// <summary>
    /// Type of change: "deprecated", "unavailable", "restored", "swapped", "fallback_activated".
    /// </summary>
    public string ChangeType { get; set; } = default!;

    /// <summary>
    /// Previous model ID (for swap operations).
    /// </summary>
    public string? PreviousModelId { get; set; }

    /// <summary>
    /// New model ID (for swap operations).
    /// </summary>
    public string? NewModelId { get; set; }

    /// <summary>
    /// Strategy affected by the change (if applicable).
    /// </summary>
    public string? AffectedStrategy { get; set; }

    /// <summary>
    /// Reason for the change.
    /// </summary>
    public string Reason { get; set; } = default!;

    /// <summary>
    /// Whether the change was automatic (by job) or manual (by admin).
    /// </summary>
    public bool IsAutomatic { get; set; }

    /// <summary>
    /// Admin user ID if change was manual. Null for automatic changes.
    /// </summary>
    public Guid? ChangedByUserId { get; set; }

    /// <summary>
    /// When the change occurred.
    /// </summary>
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
