using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Invalidates the cached projections that depend on
/// <see cref="Domain.Aggregates.MechanicAnalysisMetrics"/> after a mass-recalc job
/// finishes (ADR-051 Sprint 2 / Task 13).
/// </summary>
/// <remarks>
/// <para>
/// Two cache namespaces are evicted by tag:
/// <list type="bullet">
///   <item><description><c>mechanic-validation-dashboard</c> — bucket used by
///     <see cref="Application.Queries.MechanicValidation.GetDashboardQueryHandler"/> for
///     the admin per-game summary list.</description></item>
///   <item><description><c>mechanic-validation-trend</c> — bucket used by
///     <see cref="Application.Queries.MechanicValidation.GetTrendQueryHandler"/> for the
///     historical trend chart (per-game tag also exists; the recalc affects every game,
///     so the broader tag is the right hammer).</description></item>
/// </list>
/// </para>
/// <para>
/// The handler is best-effort: a transient cache outage must not poison the worker pool.
/// Each tag is invalidated under its own try/catch and the failure is logged at warning;
/// stale data will self-heal on the next L1/L2 TTL expiry (5 min for the dashboard).
/// This mirrors the resilience contract used by <c>VectorDocumentIndexedForKbFlagHandler</c>
/// — except we go further by isolating each tag so a failure on one tag does not skip the
/// other.
/// </para>
/// <para>
/// Multi-replica caveat (epic library-to-game CR-I2, also documented on the KB-flag
/// handler): <see cref="IHybridCacheService.RemoveByTagAsync"/> evicts the L2
/// distributed entry shared across nodes plus the L1 of <em>this</em> instance only.
/// Other replicas keep serving from their own L1 until <c>LocalCacheExpiration</c>
/// elapses — acceptable for staging (single node) and for the dashboard's 5-minute TTL,
/// which already bounds the staleness window tighter than the worker's poll cadence.
/// </para>
/// </remarks>
internal sealed class MechanicMetricsRecalculatedCacheInvalidationHandler
    : INotificationHandler<MechanicMetricsRecalculatedEvent>
{
    internal const string DashboardTag = "mechanic-validation-dashboard";
    internal const string TrendTag = "mechanic-validation-trend";

    private readonly IHybridCacheService _cache;
    private readonly ILogger<MechanicMetricsRecalculatedCacheInvalidationHandler> _logger;

    public MechanicMetricsRecalculatedCacheInvalidationHandler(
        IHybridCacheService cache,
        ILogger<MechanicMetricsRecalculatedCacheInvalidationHandler> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(MechanicMetricsRecalculatedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await InvalidateAsync(DashboardTag, notification, cancellationToken).ConfigureAwait(false);
        await InvalidateAsync(TrendTag, notification, cancellationToken).ConfigureAwait(false);
    }

    private async Task InvalidateAsync(string tag, MechanicMetricsRecalculatedEvent evt, CancellationToken ct)
    {
        try
        {
            await _cache.RemoveByTagAsync(tag, ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Cache invalidation must never poison the worker pool. The dashboard TTL (5 min)
        // bounds the staleness if eviction fails — admin will see fresh numbers on the
        // next periodic reload anyway.
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to invalidate cache tag '{Tag}' after mass recalc job {JobId} ({Status}). "
                + "Cached projections will refresh on next TTL expiry.",
                tag, evt.JobId, evt.Status);
        }
#pragma warning restore CA1031
    }
}
