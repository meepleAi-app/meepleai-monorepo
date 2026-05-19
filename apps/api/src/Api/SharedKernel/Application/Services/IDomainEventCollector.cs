using Api.SharedKernel.Domain.Interfaces;

namespace Api.SharedKernel.Application.Services;

/// <summary>
/// Service for collecting domain events from aggregates before persistence.
/// Since domain aggregates are not tracked by EF Core (only persistence entities are),
/// repositories must explicitly collect events from domain aggregates and register them
/// with this collector so they can be dispatched after SaveChangesAsync.
/// </summary>
public interface IDomainEventCollector
{
    /// <summary>
    /// Collects domain events from an aggregate root.
    /// Should be called by repositories before saving persistence entities.
    /// </summary>
    void CollectEventsFrom(IAggregateRoot aggregate);

    /// <summary>
    /// Gets all collected events and clears the collection.
    /// Called by DbContext after successful save.
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="PeekEvents"/> + <see cref="Clear"/> when you need
    /// to inspect events before deciding whether to drain them — see issue
    /// #661 spec §3.2a (non-destructive snapshot lets the DbContext persist
    /// log rows atomically with the aggregate save and recover events on
    /// failure).
    /// </remarks>
    IReadOnlyList<IDomainEvent> GetAndClearEvents();

    /// <summary>
    /// Returns a non-destructive snapshot of the currently collected events.
    /// Multiple calls return the same set until <see cref="Clear"/> is called.
    /// Issue #661 §3.2a: enables the atomic-save flow in MeepleAiDbContext —
    /// log entities are materialized from the snapshot BEFORE
    /// <c>SaveChangesAsync</c> so they commit in the same transaction.
    /// </summary>
    IReadOnlyList<IDomainEvent> PeekEvents();

    /// <summary>
    /// Drops the collected events. Called by the DbContext only after a
    /// successful <c>SaveChangesAsync</c>; on save failure the events stay
    /// queued so a subsequent retry can re-attempt them.
    /// </summary>
    void Clear();
}
