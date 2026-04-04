using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Repositories;

/// <summary>
/// EF Core repository implementation for BatchJob aggregate (Issue #3693)
/// </summary>
internal sealed class BatchJobRepository : RepositoryBase, IBatchJobRepository
{

    public BatchJobRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<BatchJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.BatchJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity;
    }

    public async Task<List<BatchJob>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.BatchJobs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<BatchJob>> GetByStatusAsync(JobStatus status, CancellationToken cancellationToken = default)
    {
        return await DbContext.BatchJobs
            .AsNoTracking()
            .Where(x => x.Status == status)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<BatchJob>> GetPagedAsync(int skip, int take, JobStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = DbContext.BatchJobs.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CountByStatusAsync(JobStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = DbContext.BatchJobs.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        return await query.CountAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(BatchJob batchJob, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batchJob);
        await DbContext.BatchJobs.AddAsync(batchJob, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(BatchJob batchJob, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batchJob);

        // Detach any previously tracked entity with the same key to avoid conflicts.
        // This happens when AddAsync tracked the original, then GetByIdAsync (AsNoTracking)
        // returns a new detached instance, and we try to Update the new instance.
        var existingTracked = DbContext.ChangeTracker.Entries<BatchJob>()
            .FirstOrDefault(e => e.Entity.Id == batchJob.Id);
        if (existingTracked != null)
        {
            existingTracked.State = EntityState.Detached;
        }

        DbContext.BatchJobs.Update(batchJob);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await DbContext.BatchJobs.FindAsync(new object[] { id }, cancellationToken).ConfigureAwait(false);
        if (job != null)
        {
            DbContext.BatchJobs.Remove(job);
            await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
