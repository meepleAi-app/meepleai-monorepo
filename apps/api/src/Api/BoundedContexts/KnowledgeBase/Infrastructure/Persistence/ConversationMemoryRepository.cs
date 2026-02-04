using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of ConversationMemory repository.
/// Issue #3493: PostgreSQL Schema Extensions for Multi-Agent System.
/// </summary>
internal class ConversationMemoryRepository : RepositoryBase, IConversationMemoryRepository
{
    public ConversationMemoryRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<List<ConversationMemory>> GetBySessionIdAsync(
        Guid sessionId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<ConversationMemoryEntity>()
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<List<ConversationMemory>> GetByUserAndGameAsync(
        Guid userId,
        Guid gameId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<ConversationMemoryEntity>()
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.GameId == gameId)
            .OrderByDescending(m => m.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<List<ConversationMemory>> GetRecentByUserIdAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<ConversationMemoryEntity>()
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<int> CountByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<ConversationMemoryEntity>()
            .CountAsync(m => m.UserId == userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(ConversationMemory memory, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(memory);
        await DbContext.Set<ConversationMemoryEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddRangeAsync(IEnumerable<ConversationMemory> memories, CancellationToken cancellationToken = default)
    {
        var entities = memories.Select(MapToEntity).ToList();
        await DbContext.Set<ConversationMemoryEntity>().AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<ConversationMemoryEntity>()
            .Where(m => m.Timestamp < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    // ========== Mapping Methods ==========

    private static ConversationMemory MapToDomain(ConversationMemoryEntity entity)
    {
        Vector? embedding = null;
        if (entity.Embedding != null)
        {
            // Issue #3547: Convert Pgvector.Vector → float[] → Domain.Vector
            embedding = new Vector(entity.Embedding.ToArray());
        }

        return new ConversationMemory(
            id: entity.Id,
            sessionId: entity.SessionId,
            userId: entity.UserId,
            gameId: entity.GameId,
            content: entity.Content,
            messageType: entity.MessageType,
            timestamp: entity.Timestamp,
            embedding: embedding);
    }

    private static ConversationMemoryEntity MapToEntity(ConversationMemory memory)
    {
        return new ConversationMemoryEntity
        {
            Id = memory.Id,
            SessionId = memory.SessionId,
            UserId = memory.UserId,
            GameId = memory.GameId,
            Content = memory.Content,
            MessageType = memory.MessageType,
            Timestamp = memory.Timestamp,
            // Issue #3547: Convert Domain.Vector → float[] → Pgvector.Vector
            Embedding = memory.Embedding != null ? new Pgvector.Vector(memory.Embedding.Values) : null
        };
    }
}
