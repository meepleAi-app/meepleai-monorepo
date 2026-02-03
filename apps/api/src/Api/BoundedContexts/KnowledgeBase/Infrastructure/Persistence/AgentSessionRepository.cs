using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Mappers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Repository for AgentSession aggregate in KnowledgeBase bounded context.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// </summary>
internal class AgentSessionRepository : RepositoryBase, IAgentSessionRepository
{
    public AgentSessionRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<AgentSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<AgentSessionEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? KnowledgeBaseMappers.ToDomain(entity) : null;
    }

    public async Task<List<AgentSession>> GetActiveByGameSessionAsync(Guid gameSessionId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<AgentSessionEntity>()
            .AsNoTracking()
            .Where(a => a.GameSessionId == gameSessionId && a.IsActive)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(KnowledgeBaseMappers.ToDomain).ToList();
    }

    public async Task<List<AgentSession>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<AgentSessionEntity>()
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(KnowledgeBaseMappers.ToDomain).ToList();
    }

    public async Task AddAsync(AgentSession agentSession, CancellationToken cancellationToken = default)
    {
        var entity = KnowledgeBaseMappers.ToEntity(agentSession);
        await DbContext.Set<AgentSessionEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
        CollectDomainEvents(agentSession);
    }

    public async Task UpdateAsync(AgentSession agentSession, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<AgentSessionEntity>()
            .FirstOrDefaultAsync(a => a.Id == agentSession.Id, cancellationToken).ConfigureAwait(false);

        if (entity == null)
        {
            throw new NotFoundException("AgentSession", agentSession.Id.ToString());
        }

        entity.CurrentGameStateJson = agentSession.CurrentGameState.ToJson();
        entity.IsActive = agentSession.IsActive;
        entity.EndedAt = agentSession.EndedAt;

        CollectDomainEvents(agentSession);
    }

    public async Task<bool> HasActiveSessionAsync(Guid gameSessionId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<AgentSessionEntity>()
            .AnyAsync(a => a.GameSessionId == gameSessionId && a.IsActive, cancellationToken).ConfigureAwait(false);
    }
}
