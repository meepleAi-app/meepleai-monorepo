using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.SharedKernel.Domain.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Raised when a <see cref="Aggregates.MechanicRecalcJob"/> reaches a terminal state
/// (Completed / Cancelled / Failed) inside <c>MechanicRecalcBackgroundService</c>
/// (ADR-051 Sprint 2 / Task 13).
/// </summary>
/// <remarks>
/// <para>
/// The mass-recalc loop refreshes
/// <see cref="Aggregates.MechanicAnalysisMetrics"/> snapshots that back the admin
/// validation dashboard and per-game trend charts. Those views are cached via the hybrid
/// cache service (<c>IHybridCacheService</c> in the Application layer) with the tags
/// <c>mechanic-validation-dashboard</c> and <c>mechanic-validation-trend</c> — without
/// invalidation, admins would keep seeing pre-recalc numbers until the L1/L2 TTLs
/// (5 min / longer) expire on their own. This event drives that invalidation.
/// </para>
/// <para>
/// Published explicitly by the worker via <see cref="MediatR.IPublisher"/> rather than
/// raised on the aggregate — the worker uses <c>Reconstitute</c> + per-iteration scopes
/// for persistence, so domain events attached to the aggregate would not survive the
/// scope hops. This mirrors the worker's existing audit-logging pattern (Task 12) which
/// also resolves a scoped service per terminal transition.
/// </para>
/// <para>
/// The payload carries enough context for handlers to scope their reactions: a handler
/// that only cares about successful runs can short-circuit on
/// <c>Status != RecalcJobStatus.Completed</c>; a handler that wants to surface
/// "0 processed because all skipped" telemetry can read the counters directly without
/// reloading the aggregate.
/// </para>
/// </remarks>
public sealed record MechanicMetricsRecalculatedEvent(
    Guid JobId,
    Guid TriggeredByUserId,
    RecalcJobStatus Status,
    int Processed,
    int Failed,
    int Skipped,
    int Total,
    DateTimeOffset CompletedAt) : IDomainEvent
{
    /// <inheritdoc />
    public DateTime OccurredAt { get; } = TimeProvider.System.GetUtcNow().UtcDateTime;

    /// <inheritdoc />
    public Guid EventId { get; } = Guid.NewGuid();
}
