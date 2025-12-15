namespace Api.SharedKernel.Domain.Interfaces;

/// <summary>
/// Marker interface for aggregate roots in DDD.
/// An aggregate root is an entity that acts as the entry point to an aggregate.
/// All operations on the aggregate must go through the aggregate root.
/// </summary>
internal interface IAggregateRoot
{
    /// <summary>
    /// Gets the collection of domain events raised by this aggregate root.
    /// </summary>
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    /// <summary>
    /// Clears all domain events from this aggregate root.
    /// Called after domain events have been dispatched.
    /// </summary>
    void ClearDomainEvents();
}
