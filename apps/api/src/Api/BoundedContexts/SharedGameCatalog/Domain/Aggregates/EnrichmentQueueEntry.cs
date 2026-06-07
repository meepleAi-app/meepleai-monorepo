using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

/// <summary>
/// Aggregate root for a single queued enrichment request (#1874).
/// Each entry represents one BGG enrichment job waiting to be picked up by
/// <c>BggImportQueueBackgroundService</c>. Multiple entries for the same
/// <see cref="SharedGameId"/> are permitted (legitimate re-queue scenarios).
/// </summary>
/// <remarks>
/// Lifecycle: Enqueue → MarkProcessed (idempotent).
/// Surfaced by the admin Queue Pending panel in /admin/catalog-ingestion.
/// </remarks>
public sealed class EnrichmentQueueEntry : AggregateRoot<Guid>
{
    // === Identity / metadata ===

    public Guid SharedGameId { get; private set; }

    public EnrichmentPriority Priority { get; private set; }

    public DateTimeOffset QueuedAt { get; private set; }

    /// <summary>Human-readable reason (e.g. "v2.1 errata", "stale 30gg", "manual retry").</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>User who queued the entry; <c>null</c> for system / cron / background sweeps.</summary>
    public Guid? QueuedByUserId { get; private set; }

    // === Lifecycle state ===

    public bool IsProcessed { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    // ===================================================
    // Constructors
    // ===================================================

    /// <summary>EF Core / repository reconstitution — do not call directly.</summary>
    private EnrichmentQueueEntry() : base() { }

    private EnrichmentQueueEntry(
        Guid id,
        Guid sharedGameId,
        EnrichmentPriority priority,
        string reason,
        Guid? queuedByUserId,
        DateTimeOffset queuedAt)
        : base(id)
    {
        SharedGameId = sharedGameId;
        Priority = priority;
        Reason = reason;
        QueuedByUserId = queuedByUserId;
        QueuedAt = queuedAt;
        IsProcessed = false;
    }

    // ===================================================
    // Factory
    // ===================================================

    /// <summary>
    /// Queues a new enrichment request for <paramref name="sharedGameId"/> at the given priority.
    /// </summary>
    /// <param name="sharedGameId">Target shared game. Must not be <see cref="Guid.Empty"/>.</param>
    /// <param name="priority">High / Normal / Stale.</param>
    /// <param name="reason">Non-empty rationale (≤ 200 chars).</param>
    /// <param name="queuedBy">User id; <c>null</c> for system/cron-triggered batches.</param>
    public static EnrichmentQueueEntry Enqueue(
        Guid sharedGameId,
        EnrichmentPriority priority,
        string reason,
        Guid? queuedBy)
    {
        if (sharedGameId == Guid.Empty)
        {
            throw new ArgumentException("SharedGameId cannot be Guid.Empty.", nameof(sharedGameId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason is required.", nameof(reason));
        }

        if (reason.Length > 200)
        {
            throw new ArgumentException("Reason must be 200 characters or fewer.", nameof(reason));
        }

        if (queuedBy.HasValue && queuedBy.Value == Guid.Empty)
        {
            throw new ArgumentException("QueuedByUserId cannot be Guid.Empty (use null for system).", nameof(queuedBy));
        }

        return new EnrichmentQueueEntry(
            id: Guid.NewGuid(),
            sharedGameId: sharedGameId,
            priority: priority,
            reason: reason,
            queuedByUserId: queuedBy,
            queuedAt: DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Repository hydration — bypasses invariants. Caller must guarantee state validity.
    /// </summary>
    public static EnrichmentQueueEntry Reconstitute(
        Guid id,
        Guid sharedGameId,
        EnrichmentPriority priority,
        string reason,
        Guid? queuedByUserId,
        DateTimeOffset queuedAt,
        bool isProcessed,
        DateTimeOffset? processedAt)
    {
        return new EnrichmentQueueEntry
        {
            Id = id,
            SharedGameId = sharedGameId,
            Priority = priority,
            Reason = reason,
            QueuedByUserId = queuedByUserId,
            QueuedAt = queuedAt,
            IsProcessed = isProcessed,
            ProcessedAt = processedAt,
        };
    }

    // ===================================================
    // Lifecycle
    // ===================================================

    /// <summary>
    /// Marks the entry as processed. Idempotent — re-calling is a no-op.
    /// Stamps <see cref="ProcessedAt"/> on first call.
    /// </summary>
    public void MarkProcessed()
    {
        if (IsProcessed) return;
        IsProcessed = true;
        ProcessedAt = DateTimeOffset.UtcNow;
    }
}
