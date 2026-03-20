using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for SessionEvent aggregate.
/// Uses MeepleAiDbContext with persistence entities, maps to/from domain entities.
/// Issue #276 - Session Diary / Timeline
/// </summary>
public class SessionEventRepository : RepositoryBase, ISessionEventRepository
{
    public SessionEventRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SessionEvent>> GetBySessionIdAsync(
        Guid sessionId,
        string? eventType = null,
        int limit = 50,
        int offset = 0,
        CancellationToken ct = default)
    {
        var query = DbContext.SessionEvents
            .AsNoTracking()
            .Where(e => e.SessionId == sessionId && !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(e => e.EventType == eventType);
        }

        var entities = await query
            .OrderByDescending(e => e.Timestamp)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(SessionEventMapper.ToDomain);
    }

    /// <inheritdoc />
    public async Task<SessionEvent?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await DbContext.SessionEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct)
            .ConfigureAwait(false);

        return entity == null ? null : SessionEventMapper.ToDomain(entity);
    }

    /// <inheritdoc />
    public async Task<int> CountBySessionIdAsync(Guid sessionId, string? eventType = null, CancellationToken ct = default)
    {
        var query = DbContext.SessionEvents
            .AsNoTracking()
            .Where(e => e.SessionId == sessionId && !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(e => e.EventType == eventType);
        }

        return await query.CountAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddAsync(SessionEvent sessionEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sessionEvent);

        var entity = SessionEventMapper.ToEntity(sessionEvent);
        await DbContext.SessionEvents.AddAsync(entity, ct).ConfigureAwait(false);
    }
}
