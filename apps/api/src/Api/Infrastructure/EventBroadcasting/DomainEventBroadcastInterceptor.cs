using Api.BoundedContexts.Administration.Application.Queries.AdminEvents;
using Api.Infrastructure.Entities.DomainEventLog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Api.Infrastructure.EventBroadcasting;

/// <summary>
/// EF Core <see cref="SaveChangesInterceptor"/> that publishes newly committed
/// <see cref="DomainEventLogEntity"/> rows to the in-process
/// <see cref="IEventBroadcaster"/> AFTER each successful <c>SaveChangesAsync</c>.
///
/// <para>
/// F4.1 issue #1718 — this is the glue between the outbox written by
/// <see cref="Api.Infrastructure.MeepleAiDbContext.SaveChangesAsync"/> (Issue #661,
/// S1 PR #1532) and the SSE stream served by <c>AdminEventsEndpoints</c>.
/// </para>
///
/// <para>
/// <b>Lifecycle:</b><br/>
/// 1. <see cref="SavingChangesAsync"/> fires BEFORE the SQL transaction commits.
///    We snapshot the set of <c>EntityState.Added</c>
///    <see cref="DomainEventLogEntity"/> instances from the change tracker at this
///    point so we don't need to touch the tracker again after the commit.<br/>
/// 2. <see cref="SavedChangesAsync"/> fires AFTER the SQL succeeds.
///    We iterate the snapshot and invoke <see cref="IEventBroadcaster.Publish"/>
///    for each row — one publish per new log entity.<br/>
/// 3. <see cref="SaveChangesFailedAsync"/> fires if the SQL throws.
///    We clear the snapshot without publishing — transactional safety: nothing
///    is broadcast for a rolled-back commit.
/// </para>
///
/// <para>
/// <b>Scoped state:</b><br/>
/// The interceptor is registered as <em>scoped</em> (one per HTTP request / DI scope)
/// by <c>EventBroadcastingExtensions.AddEventBroadcasting()</c>. Because a
/// <see cref="DbContext"/> is also scoped, there is at most one interceptor instance
/// per context lifetime. It is therefore safe to store the captured entities as an
/// instance-level list — no cross-context or cross-thread contamination is possible.
/// </para>
///
/// <para>
/// <b>DI registration (Task 1.6):</b><br/>
/// This interceptor is wired to EF via
/// <c>AddDbContext((sp, opts) =&gt; opts.AddInterceptors(sp.GetRequiredService&lt;DomainEventBroadcastInterceptor&gt;()))</c>
/// in <c>EventBroadcastingExtensions.AddEventBroadcasting()</c>.
/// </para>
/// </summary>
internal sealed class DomainEventBroadcastInterceptor : SaveChangesInterceptor
{
    private readonly IEventBroadcaster _broadcaster;

    /// <summary>
    /// Per-SaveChanges call snapshot of <see cref="DomainEventLogEntity"/> instances
    /// whose <c>EntityState</c> was <c>Added</c> at the time <see cref="SavingChangesAsync"/>
    /// fired. Cleared on each entry to <see cref="SavingChangesAsync"/> and again in
    /// <see cref="SaveChangesFailedAsync"/> to guard against stale data.
    /// </summary>
    /// <remarks>
    /// We capture instances (not DTOs) here so the mapping to <see cref="DomainEventDto"/>
    /// happens in <see cref="SavedChangesAsync"/> — by that point EF has populated all
    /// DB-generated values (e.g. database-defaulted columns), giving us the final row state.
    /// </remarks>
    private List<DomainEventLogEntity> _pendingEntities = [];

    /// <summary>
    /// Initialises the interceptor with the in-process event broadcaster.
    /// </summary>
    /// <param name="broadcaster">
    /// The <see cref="IEventBroadcaster"/> singleton that fans out events to active
    /// SSE subscribers. Injected via DI.
    /// </param>
    public DomainEventBroadcastInterceptor(IEventBroadcaster broadcaster)
    {
        ArgumentNullException.ThrowIfNull(broadcaster);
        _broadcaster = broadcaster;
    }

    // -------------------------------------------------------------------------
    // EF Core hooks
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fires BEFORE the SQL transaction is committed.
    /// Snapshots all <see cref="DomainEventLogEntity"/> instances that are about to be
    /// inserted (EntityState.Added) from the change tracker.
    /// </summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        // Reset from any previous SaveChanges call on this interceptor instance.
        _pendingEntities = [];

        var ctx = eventData.Context;
        if (ctx is not null)
        {
            _pendingEntities = ctx.ChangeTracker
                .Entries<DomainEventLogEntity>()
                .Where(e => e.State == EntityState.Added)
                .Select(e => e.Entity)
                .ToList();
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Fires AFTER the SQL transaction commits successfully.
    /// Projects each captured <see cref="DomainEventLogEntity"/> to a
    /// <see cref="DomainEventDto"/> and publishes it via <see cref="IEventBroadcaster"/>.
    /// </summary>
    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        // Snapshot and clear before publishing so any exception in Publish
        // doesn't leave stale state on the next SaveChanges call.
        var toPublish = _pendingEntities;
        _pendingEntities = [];

        foreach (var entity in toPublish)
        {
            var dto = ToDto(entity);
            _broadcaster.Publish(dto);
        }

        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Fires if the SQL transaction throws (e.g., constraint violation, deadlock).
    /// Clears the snapshot without publishing — transactional safety: a rolled-back
    /// commit must not broadcast phantom events.
    /// </summary>
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pendingEntities = [];
        base.SaveChangesFailed(eventData);
    }

    /// <summary>
    /// Async variant of <see cref="SaveChangesFailed"/>. Clears pending entities
    /// without publishing on commit failure.
    /// </summary>
    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _pendingEntities = [];
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    // -------------------------------------------------------------------------
    // Mapping
    // -------------------------------------------------------------------------

    /// <summary>
    /// Straight projection from <see cref="DomainEventLogEntity"/> to
    /// <see cref="DomainEventDto"/>. No enrichment, no transformation.
    /// </summary>
    private static DomainEventDto ToDto(DomainEventLogEntity entity)
        => new(
            Id: entity.Id,
            EventId: entity.EventId,
            EventType: entity.EventType,
            AggregateType: entity.AggregateType,
            AggregateId: entity.AggregateId,
            UserId: entity.UserId,
            PayloadJson: entity.PayloadJson,
            PayloadVersion: entity.PayloadVersion,
            OccurredAt: entity.OccurredAt,
            LoggedAt: entity.LoggedAt
        );
}
