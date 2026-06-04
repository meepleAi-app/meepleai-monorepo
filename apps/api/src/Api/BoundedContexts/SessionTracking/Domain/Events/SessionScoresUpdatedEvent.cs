using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.SharedKernel.Domain.Interfaces;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Domain event raised when <see cref="Entities.Session.SetScores"/> updates the
/// session's polymorphic score data.
///
/// <para>Asse A semantic alignment #1896 (T9, DEC-1): records the moment the
/// session's <c>ScoringType</c> + <c>ScoreData</c> were (re)assigned. Downstream
/// consumers may react to score updates for invariante #15 (GameNight aggregate
/// state transitions) or for notification flows. Currently no handler is wired;
/// T10 will integrate this event with the <c>SaveSessionCommandHandler</c> path
/// that performs FluentValidation against the polymorphic scoring strategies.</para>
///
/// <para>Mapper conventions (consistent with <see cref="SessionStartedDomainEvent"/>,
/// <see cref="SessionCreatedEvent"/>, <see cref="SessionFinalizedEvent"/>): AggregateType
/// is derived from the type name ("SessionScoresUpdated"); <see cref="AggregateId"/>
/// aliases <see cref="SessionId"/>; payload is camelCase.</para>
/// </summary>
public record SessionScoresUpdatedEvent(
    Guid SessionId,
    ScoreType ScoringType,
    string ScoreData) : IDomainEvent
{
    /// <summary>
    /// IDomainEvent occurrence time — set to UtcNow at construction.
    /// </summary>
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Unique identifier for this event instance (IDomainEvent — idempotency).
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Aggregate id read by the DomainEventLog mapper — alias over <see cref="SessionId"/>.
    /// </summary>
    public Guid AggregateId => SessionId;
}
