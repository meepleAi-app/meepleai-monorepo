using Api.SharedKernel.Domain.Interfaces;

namespace Api.SharedKernel.Domain.Events;

/// <summary>
/// Base class for domain events providing common properties.
/// All domain events should inherit from this class to ensure consistent behavior.
/// </summary>
public abstract class DomainEventBase : IDomainEvent
{
    /// <summary>
    /// Gets the date and time when this event occurred.
    /// </summary>
    public DateTime OccurredAt { get; }

    /// <summary>
    /// Gets a unique identifier for this event instance.
    /// </summary>
    public Guid EventId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainEventBase"/> class.
    /// </summary>
    protected DomainEventBase()
    {
        EventId = Guid.NewGuid();
        OccurredAt = DateTime.UtcNow;
    }
}
