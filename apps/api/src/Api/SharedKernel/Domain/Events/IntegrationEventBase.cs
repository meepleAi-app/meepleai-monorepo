using Api.SharedKernel.Domain.Interfaces;

namespace Api.SharedKernel.Domain.Events;

/// <summary>
/// Base class for integration events providing common properties.
/// All integration events should inherit from this class.
/// </summary>
internal abstract class IntegrationEventBase : IIntegrationEvent
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
    /// Gets the source bounded context that published this event.
    /// </summary>
    public string SourceContext { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationEventBase"/> class.
    /// </summary>
    /// <param name="sourceContext">The name of the source bounded context</param>
    protected IntegrationEventBase(string sourceContext)
    {
        EventId = Guid.NewGuid();
        OccurredAt = TimeProvider.System.GetUtcNow().UtcDateTime;
        ArgumentNullException.ThrowIfNull(sourceContext);
        SourceContext = sourceContext;
    }
}
