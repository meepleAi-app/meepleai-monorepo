using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

internal class SessionCheckpointRepository : ISessionCheckpointRepository
{
    private readonly MeepleAiDbContext _db;

    public SessionCheckpointRepository(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<SessionCheckpoint?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.SessionCheckpoints
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct).ConfigureAwait(false);
        return entity is null ? null : SessionCheckpointMapper.ToDomain(entity);
    }

    public async Task<IEnumerable<SessionCheckpoint>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        var entities = await _db.SessionCheckpoints
            .AsNoTracking()
            .Where(e => e.SessionId == sessionId)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(ct).ConfigureAwait(false);
        return entities.Select(SessionCheckpointMapper.ToDomain);
    }

    public async Task AddAsync(SessionCheckpoint checkpoint, CancellationToken ct = default)
    {
        var entity = SessionCheckpointMapper.ToEntity(checkpoint);
        await _db.SessionCheckpoints.AddAsync(entity, ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(SessionCheckpoint checkpoint, CancellationToken ct = default)
    {
        var entity = SessionCheckpointMapper.ToEntity(checkpoint);
        _db.SessionCheckpoints.Update(entity);
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
