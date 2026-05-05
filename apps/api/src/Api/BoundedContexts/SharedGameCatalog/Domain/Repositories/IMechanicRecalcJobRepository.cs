using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository interface for the <see cref="MechanicRecalcJob"/> aggregate (ADR-051 M2.1, Sprint 2 / Task 7).
/// </summary>
/// <remarks>
/// The recalc pipeline is single-writer-per-job but can have multiple worker hosts. The
/// <see cref="ClaimNextPendingAsync"/> contract uses PostgreSQL <c>SELECT ... FOR UPDATE SKIP LOCKED</c>
/// inside a transaction so concurrent claimers never collide on the same job.
/// </remarks>
public interface IMechanicRecalcJobRepository
{
    /// <summary>
    /// Stages a new <see cref="MechanicRecalcJob"/> for insertion. Persistence occurs at
    /// <c>SaveChangesAsync</c>, which also dispatches any pending domain events.
    /// </summary>
    Task AddAsync(MechanicRecalcJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a <see cref="MechanicRecalcJob"/> by its primary key, hydrating the full state
    /// (counters, timestamps, cancellation flag). Returns <c>null</c> if the row does not exist.
    /// </summary>
    Task<MechanicRecalcJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims the next <see cref="Enums.RecalcJobStatus.Pending"/> job — selecting it,
    /// transitioning it to <see cref="Enums.RecalcJobStatus.Running"/>, and stamping
    /// <c>started_at</c> in a single SQL statement protected by
    /// <c>SELECT ... FOR UPDATE SKIP LOCKED</c>. Returns <c>null</c> when no pending jobs are
    /// claimable. Concurrent callers each receive a distinct job; rows already locked by a peer
    /// are skipped.
    /// </summary>
    /// <remarks>
    /// The returned aggregate already reflects status=Running with a non-null StartedAt — callers
    /// MUST NOT call <see cref="MechanicRecalcJob.MarkRunning(int)"/> again, as the aggregate would
    /// reject that transition. The <c>total</c> counter remains 0; the worker (Task 8) is expected
    /// to set it via a follow-up update once the candidate set is enumerated.
    /// </remarks>
    Task<MechanicRecalcJob?> ClaimNextPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a previously loaded <see cref="MechanicRecalcJob"/> as modified. Intended for
    /// aggregates that were materialized via <see cref="GetByIdAsync"/> or
    /// <see cref="ClaimNextPendingAsync"/> in a no-tracking projection. Persistence occurs at
    /// <c>SaveChangesAsync</c>.
    /// </summary>
    Task UpdateAsync(MechanicRecalcJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent <paramref name="limit"/> recalc jobs ordered by
    /// <see cref="MechanicRecalcJob.CreatedAt"/> descending. Includes terminal states (Completed,
    /// Failed) so the admin dashboard can surface recent history. Read-only — uses
    /// <c>AsNoTracking</c>.
    /// </summary>
    Task<IReadOnlyList<MechanicRecalcJob>> ListRecentAsync(int limit, CancellationToken cancellationToken = default);
}
