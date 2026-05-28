using Api.SharedKernel.Domain.Interfaces;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Domain event raised when a session is finalized.
///
/// <para>BE-3 #1590: now implements <see cref="IDomainEvent"/> so it is durably persisted to
/// <c>domain_event_logs</c> (registered with alias <c>"session.finalized"</c>) for the cross-entity
/// activity rail. Adding this event to the Session aggregate's collection ALSO activates the
/// previously-dormant <c>KnowledgeBase.SessionFinalizedEventHandler</c> (cascade cleanup of
/// AgentSessions) via the MediatR dispatch in <c>MeepleAiDbContext.SaveChangesAsync</c> — that
/// handler was wired but never fired because no code raised the event into the MediatR pipeline.</para>
///
/// <para>Mapper conventions: AggregateType derived "SessionFinalized"; AggregateId/UserId read via
/// reflection; payload camelCase. <see cref="GameName"/>/<see cref="WinnerName"/> are snapshots at
/// emit time. <see cref="OccurredAt"/> and <see cref="AggregateId"/> alias the existing
/// <see cref="Timestamp"/>/<see cref="SessionId"/> so no duplicate state is stored.</para>
/// </summary>
public record SessionFinalizedEvent : IDomainEvent
{
    /// <summary>
    /// Session unique identifier.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// User who owns the session (mapper reads this for the UserId column). BE-3 #1590.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Game reference. BE-3 #1590.
    /// </summary>
    public Guid GameId { get; init; }

    /// <summary>
    /// Game title snapshot at emit time (null if unresolved). BE-3 #1590 — rail display title.
    /// </summary>
    public string? GameName { get; init; }

    /// <summary>
    /// Number of participants in the session. BE-3 #1590.
    /// </summary>
    public int PlayerCount { get; init; }

    /// <summary>
    /// Winner participant ID (if applicable).
    /// </summary>
    public Guid? WinnerId { get; init; }

    /// <summary>
    /// Winner participant display name snapshot (if applicable). BE-3 #1590 — rail detail.
    /// </summary>
    public string? WinnerName { get; init; }

    /// <summary>
    /// Final ranks for all participants (ParticipantId -> Rank).
    /// </summary>
    public IReadOnlyDictionary<Guid, int> FinalRanks { get; init; } = new Dictionary<Guid, int>();

    /// <summary>
    /// Session duration in minutes.
    /// </summary>
    public int DurationMinutes { get; init; }

    /// <summary>
    /// When the session was finalized.
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