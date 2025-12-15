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
    IReadOnlyList<IDomainEvent> GetAndClearEvents();
}
