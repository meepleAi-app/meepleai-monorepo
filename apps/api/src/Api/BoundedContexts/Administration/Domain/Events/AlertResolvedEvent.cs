using Api.SharedKernel.Domain.Interfaces;

namespace Api.BoundedContexts.Administration.Domain.Events;

/// <summary>
/// Raised when a previously-firing alert is automatically resolved because
/// its metric returned below the configured threshold for the rule's
/// duration window (Issue #1840 SP5 F4-C7).
///
/// <para>Companion to <see cref="AlertFiredEvent"/> — together they form the
/// activity-feed cards in <c>/admin/monitor?tab=alerts</c>. Wire-up of the
/// auto-resolve loop is intentionally deferred to a follow-up (the existing
/// <c>AlertingService</c> already tracks <c>IsActive</c> on
/// <c>AlertEntity</c>); for #1840 we publish this from manual TestAlert flows
/// only.</para>
/// </summary>
public sealed record AlertResolvedEvent(
    Guid RuleId,
    string RuleName,
    Guid FiredEventId,
    TimeSpan Duration,
    bool IsTest) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public Guid EventId { get; } = Guid.NewGuid();
}
