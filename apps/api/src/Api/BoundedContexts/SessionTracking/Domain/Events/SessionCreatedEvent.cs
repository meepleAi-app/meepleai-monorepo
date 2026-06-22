using Api.SharedKernel.Domain.Interfaces;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Domain event raised when a new session is created.
///
/// <para>BE-3 #1590: now implements <see cref="IDomainEvent"/> so it is durably persisted to
/// <c>domain_event_logs</c> (registered with alias <c>"session.created"</c>) and powers the
/// cross-entity activity rail. Orthogonal to the <c>session_events</c> diary "session_created"
/// row (#1590 C3 socratic resolution — same milestone, different consumers/durability).</para>
///
/// <para>Mapper conventions (<see cref="Api.Infrastructure.DomainEventLog.DomainEventLogMapper"/>):
/// AggregateType derived "SessionCreated"; AggregateId/UserId read via reflection; payload
/// camelCase. <see cref="GameName"/> is a snapshot taken at emit time (resilient to SharedGame
/// soft-delete). <see cref="OccurredAt"/> and <see cref="AggregateId"/> are aliases over the
/// existing <see cref="Timestamp"/>/<see cref="SessionId"/> so no duplicate state is stored.</para>
/// </summary>
public record SessionCreatedEvent : IDomainEvent
{
    /// <summary>
    /// Session unique identifier.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// User who created the session.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Unique session code.
    /// </summary>
    public string SessionCode { get; init; } = string.Empty;

    /// <summary>
    /// Optional game reference.
    /// </summary>
    public Guid? GameId { get; init; }

    /// <summary>
    /// Game title snapshot at emit time (null if unresolved). BE-3 #1590 — rail display title.
    /// </summary>
    public string? GameName { get; init; }

    /// <summary>
    /// Number of participants in the session at creation. BE-3 #1590.
    /// </summary>
    public int PlayerCount { get; init; }

    /// <summary>
    /// When the session was created.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Unique identifier for this event instance (IDomainEvent — idempotency, AC1).
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// IDomainEvent occurrence time — alias over <see cref="Timestamp"/>.
    /// </summary>
    public DateTime OccurredAt => Timestamp;

    /// <summary>
    /// Aggregate id read by the DomainEventLog mapper — alias over <see cref="SessionId"/>.
    /// </summary>
    public Guid AggregateId => SessionId;
}