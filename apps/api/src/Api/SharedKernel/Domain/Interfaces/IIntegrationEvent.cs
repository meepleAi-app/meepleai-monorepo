using MediatR;

namespace Api.SharedKernel.Domain.Interfaces;

/// <summary>
/// Base interface for integration events.
/// Integration events are published across bounded contexts for cross-context communication.
/// Unlike domain events (internal to a bounded context), integration events cross context boundaries.
/// </summary>
internal interface IIntegrationEvent : INotification
{
    /// <summary>
    /// Gets the date and time when this integration event occurred.
    /// </summary>
    DateTime OccurredAt { get; }

    /// <summary>
    /// Gets a unique identifier for this integration event instance.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// Gets the source bounded context that published this event.
    /// </summary>
    string SourceContext { get; }
}
