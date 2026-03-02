using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of ISessionMediaRepository.
/// Issue #4760
/// </summary>
public class SessionMediaRepository : ISessionMediaRepository
{
    private readonly MeepleAiDbContext _context;

    public SessionMediaRepository(MeepleAiDbContext context)
    {
        _context = context;
    }

    public async Task<SessionMedia?> GetByIdAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.SessionMedia
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : SessionMediaMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<SessionMedia>> GetBySessionIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.SessionMedia
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(SessionMediaMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<SessionMedia>> GetBySnapshotIdAsync(
        Guid snapshotId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.SessionMedia
            .Where(m => m.SnapshotId == snapshotId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(SessionMediaMapper.ToDomain).ToList();
    }

    public async Task<int> GetCountBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await _context.SessionMedia
            .CountAsync(m => m.SessionId == sessionId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(SessionMedia media, CancellationToken cancellationToken = default)
    {
        var entity = SessionMediaMapper.ToEntity(media);
        await _context.SessionMedia.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(SessionMedia media, CancellationToken cancellationToken = default)
    {
        var entity = SessionMediaMapper.ToEntity(media);
        var tracked = _context.ChangeTracker.Entries<Api.Infrastructure.Entities.SessionTracking.SessionMediaEntity>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);

        if (tracked is not null)
        {
            tracked.CurrentValues.SetValues(entity);
        }
        else
        {
            _context.SessionMedia.Update(entity);
        }

        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
