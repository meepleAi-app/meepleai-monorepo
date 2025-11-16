using Api.SharedKernel.Domain.Interfaces;

namespace Api.SharedKernel.Application.Services;

/// <summary>
/// Thread-safe implementation of IDomainEventCollector.
/// Collects domain events from aggregates during repository operations
/// so they can be dispatched after successful persistence.
/// </summary>
public sealed class DomainEventCollector : IDomainEventCollector
{
    private readonly List<IDomainEvent> _collectedEvents = new();
    private readonly object _lock = new();

    /// <summary>
    /// Collects domain events from an aggregate root.
    /// Thread-safe for concurrent repository operations.
    /// </summary>
    public void CollectEventsFrom(IAggregateRoot aggregate)
    {
        if (aggregate == null)
            throw new ArgumentNullException(nameof(aggregate));

        lock (_lock)
        {
            _collectedEvents.AddRange(aggregate.DomainEvents);
        }
    }

    /// <summary>
    /// Gets all collected events and clears the collection.
    /// Called by DbContext after successful save.
    /// </summary>
    public IReadOnlyList<IDomainEvent> GetAndClearEvents()
    {
        lock (_lock)
        {
            var events = _collectedEvents.ToList();
            _collectedEvents.Clear();
            return events;
        }
    }
}
