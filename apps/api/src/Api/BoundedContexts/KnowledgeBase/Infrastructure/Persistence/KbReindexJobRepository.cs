using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// EF implementation of <see cref="IKbReindexJobRepository"/>. ADR-056 UoW pattern:
/// methods stage changes; caller invokes <c>IUnitOfWork.SaveChangesAsync</c>.
/// Issue #941 / ADR-057.
/// </summary>
internal sealed class KbReindexJobRepository : IKbReindexJobRepository
{
    private readonly MeepleAiDbContext _db;

    public KbReindexJobRepository(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<KbReindexJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.KbReindexJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<KbReindexJob?> GetActiveByGameAndUserAsync(
        Guid gameId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.KbReindexJobs
            .AsNoTracking()
            .Where(j => j.GameId == gameId
                     && j.UserId == userId
                     && (j.Status == KbReindexJobStatus.Queued || j.Status == KbReindexJobStatus.Running))
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task AddAsync(KbReindexJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        var entity = MapToPersistence(job);
        await _db.KbReindexJobs.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(KbReindexJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        // Detach any existing tracked entity with the same id to avoid IdentityConflict.
        var tracked = _db.ChangeTracker.Entries<KbReindexJobEntity>()
            .FirstOrDefault(e => e.Entity.Id == job.Id);
        if (tracked != null)
            tracked.State = EntityState.Detached;

        var entity = MapToPersistence(job);
        _db.KbReindexJobs.Update(entity);
        return Task.CompletedTask;
    }

    private static KbReindexJob MapToDomain(KbReindexJobEntity entity)
    {
        return KbReindexJob.Hydrate(
            id: entity.Id,
            gameId: entity.GameId,
            userId: entity.UserId,
            status: entity.Status,
            totalPdfs: entity.TotalPdfs,
            processedPdfs: entity.ProcessedPdfs,
            createdAt: entity.CreatedAt,
            startedAt: entity.StartedAt,
            completedAt: entity.CompletedAt,
            failureReason: entity.FailureReason);
    }

    private static KbReindexJobEntity MapToPersistence(KbReindexJob job)
    {
        return new KbReindexJobEntity
        {
            Id = job.Id,
            GameId = job.GameId,
            UserId = job.UserId,
            Status = job.Status,
            TotalPdfs = job.TotalPdfs,
            ProcessedPdfs = job.ProcessedPdfs,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            FailureReason = job.FailureReason
        };
    }
}
