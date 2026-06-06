using Api.SharedKernel.Domain.Interfaces;

namespace Api.BoundedContexts.Administration.Domain.Events;

/// <summary>
/// Health status transition event. Issue #1941 / iso-2 promoted from <c>INotification</c>
/// to <see cref="IDomainEvent"/> so the dispatcher dedup guard can short-circuit re-dispatched
/// alerts (collateral pattern shared with CF-1 AutoAgentCreationFailedEvent + CF-2
/// TokenUsageLedgerEvent). Backward compat: positional ctor unchanged; the <see cref="EventId"/>
/// and <see cref="OccurredAt"/> properties auto-initialise so existing publishers compile.
/// </summary>
public record HealthStatusChangedEvent(
    string ServiceName,
    string PreviousStatus,
    string CurrentStatus,
    string? Description,
    string[] Tags,
    DateTime TransitionAt,
    bool IsReminder
) : IDomainEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
