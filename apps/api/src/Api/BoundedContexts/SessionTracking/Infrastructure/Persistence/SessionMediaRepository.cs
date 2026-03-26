using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of ISessionMediaRepository.
/// Issue #4760
/// </summary>
public class SessionMediaRepository : RepositoryBase, ISessionMediaRepository
{
    public SessionMediaRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<SessionMedia?> GetByIdAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.SessionMedia
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : SessionMediaMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<SessionMedia>> GetBySessionIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.SessionMedia
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
        var entities = await DbContext.SessionMedia
            .Where(m => m.SnapshotId == snapshotId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(SessionMediaMapper.ToDomain).ToList();
    }

    public async Task<int> GetCountBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await DbContext.SessionMedia
            .CountAsync(m => m.SessionId == sessionId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(SessionMedia media, CancellationToken cancellationToken = default)
    {
        var entity = SessionMediaMapper.ToEntity(media);
        await DbContext.SessionMedia.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(SessionMedia media, CancellationToken cancellationToken = default)
    {
        var entity = SessionMediaMapper.ToEntity(media);
        var tracked = DbContext.ChangeTracker.Entries<Api.Infrastructure.Entities.SessionTracking.SessionMediaEntity>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);

        if (tracked is not null)
        {
            tracked.CurrentValues.SetValues(entity);
        }
        else
        {
            DbContext.SessionMedia.Update(entity);
        }

        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
