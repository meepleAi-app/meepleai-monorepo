using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IVisionSnapshotRepository.
/// Uses direct domain entity mapping (no separate persistence entity).
/// Session Vision AI feature.
/// </summary>
internal sealed class VisionSnapshotRepository : RepositoryBase, IVisionSnapshotRepository
{
    public VisionSnapshotRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<VisionSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await DbContext.VisionSnapshots
            .Include(s => s.Images.OrderBy(i => i.OrderIndex))
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            .ConfigureAwait(false);
    }

    public async Task<List<VisionSnapshot>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await DbContext.VisionSnapshots
            .Include(s => s.Images.OrderBy(i => i.OrderIndex))
            .Where(s => s.SessionId == sessionId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<VisionSnapshot?> GetLatestBySessionIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await DbContext.VisionSnapshots
            .Include(s => s.Images.OrderBy(i => i.OrderIndex))
            .Where(s => s.SessionId == sessionId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(VisionSnapshot snapshot, CancellationToken ct = default)
    {
        await DbContext.VisionSnapshots
            .AddAsync(snapshot, ct)
            .ConfigureAwait(false);
    }

    public Task UpdateAsync(VisionSnapshot snapshot, CancellationToken ct = default)
    {
        DbContext.VisionSnapshots.Update(snapshot);
        return Task.CompletedTask;
    }

    public async Task<int> CountBySessionIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await DbContext.VisionSnapshots
            .CountAsync(s => s.SessionId == sessionId, ct)
            .ConfigureAwait(false);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await DbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
