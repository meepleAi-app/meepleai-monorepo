using Api.SharedKernel.Domain.Interfaces;

namespace Api.SharedKernel.Application.Services;

/// <summary>
/// Thread-safe implementation of IDomainEventCollector.
/// Collects domain events from aggregates during repository operations
/// so they can be dispatched after successful persistence.
/// </summary>
internal sealed class DomainEventCollector : IDomainEventCollector
{
#pragma warning disable MA0158 // Use System.Threading.Lock
    private readonly List<IDomainEvent> _collectedEvents = new();
    private readonly object _lock = new();
#pragma warning restore MA0158

    /// <summary>
    /// Collects domain events from an aggregate root.
    /// Thread-safe for concurrent repository operations.
    /// </summary>
    public void CollectEventsFrom(IAggregateRoot aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);


        lock (_lock)
        {
            _collectedEvents.AddRange(aggregate.DomainEvents);
        }
    }

    /// <summary>
    /// Directly enqueues a single domain event.
    /// See <see cref="IDomainEventCollector.Collect"/>.
    /// </summary>
    public void Collect(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        lock (_lock)
        {
            _collectedEvents.Add(domainEvent);
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

    /// <summary>
    /// Non-destructive snapshot. See <see cref="IDomainEventCollector.PeekEvents"/>.
    /// </summary>
    public IReadOnlyList<IDomainEvent> PeekEvents()
    {
        lock (_lock)
        {
            return _collectedEvents.ToList();
        }
    }

    /// <summary>
    /// Drops the collected events. See <see cref="IDomainEventCollector.Clear"/>.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _collectedEvents.Clear();
        }
    }
}
