namespace Api.SharedKernel.Domain.Interfaces;

/// <summary>
/// Base interface for domain events.
/// Domain events represent something that happened in the domain that other parts of the system should know about.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Gets the date and time when this event occurred.
    /// </summary>
    DateTime OccurredAt { get; }

    /// <summary>
    /// Gets a unique identifier for this event instance.
    /// </summary>
    Guid EventId { get; }
}
