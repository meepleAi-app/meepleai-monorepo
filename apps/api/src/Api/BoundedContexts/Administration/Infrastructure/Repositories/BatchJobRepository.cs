using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Repositories;

/// <summary>
/// EF Core repository implementation for BatchJob aggregate (Issue #3693)
/// </summary>
internal sealed class BatchJobRepository : IBatchJobRepository
{
    private readonly MeepleAiDbContext _context;

    public BatchJobRepository(MeepleAiDbContext context) =>
        _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<BatchJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.BatchJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity;
    }

    public async Task<List<BatchJob>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.BatchJobs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<BatchJob>> GetByStatusAsync(JobStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.BatchJobs
            .AsNoTracking()
            .Where(x => x.Status == status)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<BatchJob>> GetPagedAsync(int skip, int take, JobStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = _context.BatchJobs.AsNoTracking();

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
        var query = _context.BatchJobs.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        return await query.CountAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(BatchJob batchJob, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batchJob);
        await _context.BatchJobs.AddAsync(batchJob, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(BatchJob batchJob, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batchJob);
        _context.BatchJobs.Update(batchJob);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _context.BatchJobs.FindAsync(new object[] { id }, cancellationToken).ConfigureAwait(false);
        if (job != null)
        {
            _context.BatchJobs.Remove(job);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
