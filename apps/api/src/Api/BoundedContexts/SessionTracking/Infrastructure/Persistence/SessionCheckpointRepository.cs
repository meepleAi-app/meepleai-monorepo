using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

internal class SessionCheckpointRepository : RepositoryBase, ISessionCheckpointRepository
{
    public SessionCheckpointRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<SessionCheckpoint?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await DbContext.SessionCheckpoints
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct).ConfigureAwait(false);
        return entity is null ? null : SessionCheckpointMapper.ToDomain(entity);
    }

    public async Task<IEnumerable<SessionCheckpoint>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        var entities = await DbContext.SessionCheckpoints
            .AsNoTracking()
            .Where(e => e.SessionId == sessionId)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(ct).ConfigureAwait(false);
        return entities.Select(SessionCheckpointMapper.ToDomain);
    }

    public async Task AddAsync(SessionCheckpoint checkpoint, CancellationToken ct = default)
    {
        var entity = SessionCheckpointMapper.ToEntity(checkpoint);
        await DbContext.SessionCheckpoints.AddAsync(entity, ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(SessionCheckpoint checkpoint, CancellationToken ct = default)
    {
        var entity = SessionCheckpointMapper.ToEntity(checkpoint);
        DbContext.SessionCheckpoints.Update(entity);
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
