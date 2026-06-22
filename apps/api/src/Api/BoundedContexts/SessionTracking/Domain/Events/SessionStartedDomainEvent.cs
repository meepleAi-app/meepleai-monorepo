using Api.SharedKernel.Domain.Interfaces;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Domain event raised when a <see cref="Entities.Session"/> transitions to live mode
/// via <see cref="Entities.Session.OpenLiveMode"/>.
///
/// <para>Asse A semantic alignment #1896 (T2): records the timestamp at which the session
/// became "live" (i.e. <c>StartedAt</c> set, <c>FinalizedAt</c> still null). Invariants
/// #11 (Session has 3 distinct timestamps: createdAt always, startedAt/completedAt nullable)
/// and #14 (max 1 live session per GameNight at any given moment) — invariant #14 is
/// enforced by GameManagement.SessionStartedHandler (T3) which validates the parent
/// GameNight has no other live Session before transitioning Published → InProgress.</para>
///
/// <para>Listener: <c>GameManagement.SessionStartedHandler</c> (added in T3) triggers
/// <c>GameNightEvent</c> transition <c>Published → InProgress</c> (invariante #15).</para>
///
/// <para>Mapper conventions (consistent with <see cref="SessionCreatedEvent"/>,
/// <see cref="SessionFinalizedEvent"/>): AggregateType derived "SessionStarted";
/// AggregateId/UserId read via reflection; payload camelCase.
/// <see cref="OccurredAt"/> and <see cref="AggregateId"/> alias the existing
/// <see cref="StartedAt"/>/<see cref="SessionId"/> so no duplicate state is stored.</para>
/// </summary>
public record SessionStartedDomainEvent : IDomainEvent
{
    /// <summary>
    /// Session unique identifier.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// User who owns the session (mapper reads this for the UserId column).
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Game reference.
    /// </summary>
    public Guid GameId { get; init; }

    /// <summary>
    /// When the session was transitioned to live mode.
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// Unique identifier for this event instance (IDomainEvent — idempotency).
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// IDomainEvent occurrence time — alias over <see cref="StartedAt"/>.
    /// </summary>
    public DateTime OccurredAt => StartedAt;

    /// <summary>
    /// Aggregate id read by the DomainEventLog mapper — alias over <see cref="SessionId"/>.
    /// </summary>
    public Guid AggregateId => SessionId;
}
