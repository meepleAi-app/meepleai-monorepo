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
    /// <remarks>
    /// <c>init</c> so the outbox deserializer can restore the stored value: without a setter the
    /// constructor's "now" would silently replace it on every delivery (#4841).
    /// </remarks>
    public DateTime OccurredAt { get; init; }

    /// <summary>
    /// Gets a unique identifier for this event instance.
    /// </summary>
    /// <remarks>
    /// <c>init</c> so the outbox deserializer restores the id that the outbox row carries as its own
    /// <c>Id</c>. With a getter-only property every redelivery got a fresh <c>Guid.NewGuid()</c>, which
    /// made every dedupe keyed on this id (<c>LastProcessedEventId</c>, <c>SourceEventId</c> under
    /// partial unique indexes) ineffective across deliveries (#4841).
    /// </remarks>
    public Guid EventId { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainEventBase"/> class.
    /// </summary>
    protected DomainEventBase()
    {
        EventId = Guid.NewGuid();
        OccurredAt = TimeProvider.System.GetUtcNow().UtcDateTime;
    }
}
