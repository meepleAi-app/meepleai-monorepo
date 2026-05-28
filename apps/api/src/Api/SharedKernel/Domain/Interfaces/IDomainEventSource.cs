namespace Api.SharedKernel.Domain.Interfaces;

/// <summary>
/// Lightweight alternative to <see cref="IAggregateRoot"/> for domain objects that need to raise
/// domain events but cannot (or should not) inherit from the <c>AggregateRoot&lt;TId&gt;</c> base class.
/// <para>
/// Extends <see cref="IAggregateRoot"/> so that any implementation satisfies the existing
/// <c>IDomainEventCollector.CollectEventsFrom</c> and <c>RepositoryBase.CollectDomainEvents</c>
/// overloads without requiring changes to the collection infrastructure.
/// </para>
/// <para>
/// BE-3 #1590: introduced to let <c>Session</c> (SessionTracking BC) participate in the
/// <c>domain_event_logs</c> pipeline without a full <c>AggregateRoot&lt;TId&gt;</c> conversion.
/// </para>
/// </summary>
public interface IDomainEventSource : IAggregateRoot
{
    /// <summary>
    /// Appends a domain event to the pending-events list.
    /// Called by domain methods immediately after a state transition (e.g.
    /// <c>Session.Create</c>, <c>Session.Finalize</c>).
    /// </summary>
    /// <param name="domainEvent">The event to enqueue. Must not be <see langword="null"/>.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="domainEvent"/> is null.</exception>
    void AddDomainEvent(IDomainEvent domainEvent);
}
