using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for the <see cref="MechanicRecalcJob"/> aggregate
/// (ADR-051 M2.1, Sprint 2 / Task 7).
/// </summary>
/// <remarks>
/// <see cref="ClaimNextPendingAsync"/> uses raw SQL with a CTE and
/// <c>FOR UPDATE SKIP LOCKED</c> to deliver an at-most-once claim primitive: concurrent worker
/// instances each receive a distinct row, peers' locked rows are silently skipped. The atomic
/// <c>UPDATE ... FROM (SELECT ... FOR UPDATE SKIP LOCKED) ... RETURNING *</c> pattern keeps the
/// SELECT lock and the status transition inside a single statement, eliminating any TOCTOU
/// window between the lock acquisition and the state mutation.
/// </remarks>
internal sealed class MechanicRecalcJobRepository : RepositoryBase, IMechanicRecalcJobRepository
{
    public MechanicRecalcJobRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task AddAsync(MechanicRecalcJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        var entity = MapToEntity(job);
        await DbContext.MechanicRecalcJobs.AddAsync(entity, cancellationToken).ConfigureAwait(false);

        CollectDomainEvents(job);
    }

    public async Task<MechanicRecalcJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.MechanicRecalcJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<MechanicRecalcJob?> ClaimNextPendingAsync(CancellationToken cancellationToken = default)
    {
        // Atomic claim: SELECT ... FOR UPDATE SKIP LOCKED inside an UPDATE CTE so the row lock
        // and the Pending → Running transition happen in a single statement. Concurrent claimers
        // each see a distinct id (or null when no Pending rows remain). RETURNING * lets EF
        // materialize the entity from the same statement so we don't need a follow-up SELECT.
        //
        // Status code 0 == Pending, 1 == Running. now() at timezone 'utc' yields a UTC timestamptz
        // matching DateTimeOffset.UtcNow semantics on the round-trip.
        const string sql = @"
            UPDATE mechanic_recalc_jobs
            SET status = 1, started_at = (now() at time zone 'utc')
            WHERE id = (
                SELECT id FROM mechanic_recalc_jobs
                WHERE status = 0
                ORDER BY created_at
                LIMIT 1
                FOR UPDATE SKIP LOCKED
            )
            RETURNING *;";

        var tx = await DbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var _ = tx.ConfigureAwait(false);

        // FromSqlRaw on a non-composable statement (UPDATE ... RETURNING) cannot have any
        // additional LINQ operator applied (EF would try to wrap it as a subquery and throw
        // 'non-composable SQL ... composing over it'). ToListAsync executes the SQL as-is and
        // yields the materialized rows; the statement returns at most one row.
        var rows = await DbContext.MechanicRecalcJobs
            .FromSqlRaw(sql)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = rows.FirstOrDefault();

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public Task UpdateAsync(MechanicRecalcJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        var entity = MapToEntity(job);
        DbContext.MechanicRecalcJobs.Update(entity);

        CollectDomainEvents(job);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<MechanicRecalcJob>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be positive.");
        }

        var entities = await DbContext.MechanicRecalcJobs
            .AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    // === Mapping ===

    private static MechanicRecalcJob MapToDomain(MechanicRecalcJobEntity entity)
    {
        return MechanicRecalcJob.Reconstitute(
            id: entity.Id,
            status: entity.Status,
            triggeredByUserId: entity.TriggeredByUserId,
            total: entity.Total,
            processed: entity.Processed,
            failed: entity.Failed,
            skipped: entity.Skipped,
            consecutiveFailures: entity.ConsecutiveFailures,
            lastError: entity.LastError,
            lastProcessedAnalysisId: entity.LastProcessedAnalysisId,
            cancellationRequested: entity.CancellationRequested,
            createdAt: entity.CreatedAt,
            startedAt: entity.StartedAt,
            completedAt: entity.CompletedAt,
            heartbeatAt: entity.HeartbeatAt);
    }

    private static MechanicRecalcJobEntity MapToEntity(MechanicRecalcJob job)
    {
        return new MechanicRecalcJobEntity
        {
            Id = job.Id,
            Status = job.Status,
            TriggeredByUserId = job.TriggeredByUserId,
            Total = job.Total,
            Processed = job.Processed,
            Failed = job.Failed,
            Skipped = job.Skipped,
            ConsecutiveFailures = job.ConsecutiveFailures,
            LastError = job.LastError,
            LastProcessedAnalysisId = job.LastProcessedAnalysisId,
            CancellationRequested = job.CancellationRequested,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            HeartbeatAt = job.HeartbeatAt,
        };
    }
}
