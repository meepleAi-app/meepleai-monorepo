using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of ISessionChatRepository.
/// Issue #4760
/// </summary>
public class SessionChatRepository : RepositoryBase, ISessionChatRepository
{
    public SessionChatRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<SessionChatMessage?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.SessionChatMessages
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : SessionChatMessageMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<SessionChatMessage>> GetBySessionIdAsync(
        Guid sessionId,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.SessionChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.SequenceNumber);

        IQueryable<Api.Infrastructure.Entities.SessionTracking.SessionChatMessageEntity> paginated = query;

        if (offset.HasValue)
            paginated = paginated.Skip(offset.Value);

        if (limit.HasValue)
            paginated = paginated.Take(limit.Value);

        var entities = await paginated
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(SessionChatMessageMapper.ToDomain).ToList();
    }

    public async Task<int> GetNextSequenceNumberAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        // Must include deleted messages to avoid sequence number reuse
        var maxSequence = await DbContext.SessionChatMessages
            .IgnoreQueryFilters()
            .Where(m => m.SessionId == sessionId)
            .MaxAsync(m => (int?)m.SequenceNumber, cancellationToken)
            .ConfigureAwait(false);

        return (maxSequence ?? 0) + 1;
    }

    public async Task<int> GetCountBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await DbContext.SessionChatMessages
            .CountAsync(m => m.SessionId == sessionId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(SessionChatMessage message, CancellationToken cancellationToken = default)
    {
        var entity = SessionChatMessageMapper.ToEntity(message);
        await DbContext.SessionChatMessages.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(SessionChatMessage message, CancellationToken cancellationToken = default)
    {
        var entity = SessionChatMessageMapper.ToEntity(message);
        var tracked = DbContext.ChangeTracker.Entries<Api.Infrastructure.Entities.SessionTracking.SessionChatMessageEntity>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);

        if (tracked is not null)
        {
            tracked.CurrentValues.SetValues(entity);
        }
        else
        {
            DbContext.SessionChatMessages.Update(entity);
        }

        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
