using System.Text.Json;
using System.Text.Json.Serialization;
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
/// EF Core implementation of ChatSession repository.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
internal class ChatSessionRepository : RepositoryBase, IChatSessionRepository
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ChatSessionRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<ChatSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<ChatSessionEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<ChatSession?> GetByIdWithPaginatedMessagesAsync(
        Guid id,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<ChatSessionEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken).ConfigureAwait(false);

        if (entity == null)
            return null;

        return MapToDomainWithPagination(entity, skip, take);
    }

    public async Task<List<ChatSession>> GetByUserAndGameAsync(
        Guid userId,
        Guid gameId,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<ChatSessionEntity>()
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.GameId == gameId && !s.IsArchived)
            .OrderByDescending(s => s.LastMessageAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomainSummary).ToList();
    }

    public async Task<List<ChatSession>> GetByUserIdAsync(
        Guid userId,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<ChatSessionEntity>()
            .AsNoTracking()
            .Where(s => s.UserId == userId && !s.IsArchived)
            .OrderByDescending(s => s.LastMessageAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomainSummary).ToList();
    }

    public async Task<List<ChatSession>> GetRecentByUserIdAsync(
        Guid userId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<ChatSessionEntity>()
            .AsNoTracking()
            .Where(s => s.UserId == userId && !s.IsArchived)
            .OrderByDescending(s => s.LastMessageAt)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomainSummary).ToList();
    }

    public async Task<int> CountByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<ChatSessionEntity>()
            .AsNoTracking()
            .CountAsync(s => s.UserId == userId && !s.IsArchived, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ChatSession?> GetOldestActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<ChatSessionEntity>()
            .AsNoTracking()
            .Where(s => s.UserId == userId && !s.IsArchived)
            .OrderBy(s => s.LastMessageAt)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomainSummary(entity) : null;
    }

    public async Task<int> CountByUserAndGameAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<ChatSessionEntity>()
            .AsNoTracking()
            .CountAsync(s => s.UserId == userId && s.GameId == gameId && !s.IsArchived, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(ChatSession session, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(session);
        var entity = MapToEntity(session);
        await DbContext.Set<ChatSessionEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(ChatSession session, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(session);
        var entity = MapToEntity(session);

        // Detach existing tracked entity to avoid conflicts
        var tracked = DbContext.ChangeTracker.Entries<ChatSessionEntity>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);

        if (tracked != null)
            tracked.State = EntityState.Detached;

        DbContext.Set<ChatSessionEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ChatSession session, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(session);
        DbContext.Set<ChatSessionEntity>().Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<ChatSessionEntity>()
            .AnyAsync(s => s.Id == id, cancellationToken).ConfigureAwait(false);
    }

    // ========== Mapping Methods ==========

    private static ChatSession MapToDomain(ChatSessionEntity entity)
    {
        var messageDtos = JsonSerializer.Deserialize<List<PersistenceMessageDto>>(entity.MessagesJson, s_jsonOptions)
            ?? new List<PersistenceMessageDto>();

        var session = CreateSessionFromEntity(entity);

        foreach (var dto in messageDtos)
        {
            var message = SessionChatMessage.FromPersistence(
                dto.Id,
                dto.Content,
                dto.Role,
                dto.SequenceNumber,
                dto.Timestamp,
                dto.MetadataJson);

            AddMessageViaReflection(session, message);
        }

        return session;
    }

    private static ChatSession MapToDomainWithPagination(ChatSessionEntity entity, int skip, int take)
    {
        var messageDtos = JsonSerializer.Deserialize<List<PersistenceMessageDto>>(entity.MessagesJson, s_jsonOptions)
            ?? new List<PersistenceMessageDto>();

        var session = CreateSessionFromEntity(entity);

        var paginatedMessages = messageDtos
            .OrderBy(m => m.SequenceNumber)
            .Skip(skip)
            .Take(take)
            .ToList();

        foreach (var dto in paginatedMessages)
        {
            var message = SessionChatMessage.FromPersistence(
                dto.Id,
                dto.Content,
                dto.Role,
                dto.SequenceNumber,
                dto.Timestamp,
                dto.MetadataJson);

            AddMessageViaReflection(session, message);
        }

        return session;
    }

    /// <summary>
    /// Maps to domain without loading full messages (for list queries).
    /// </summary>
    private static ChatSession MapToDomainSummary(ChatSessionEntity entity)
    {
        return CreateSessionFromEntity(entity);
    }

    private static ChatSession CreateSessionFromEntity(ChatSessionEntity entity)
    {
        var session = new ChatSession(
            id: entity.Id,
            userId: entity.UserId,
            gameId: entity.GameId,
            title: entity.Title,
            userLibraryEntryId: entity.UserLibraryEntryId,
            agentSessionId: entity.AgentSessionId,
            agentConfigJson: entity.AgentConfigJson,
            agentId: entity.AgentId,
            agentType: entity.AgentType,
            agentName: entity.AgentName);

        // Override timestamps from DB
        var createdAtProp = typeof(ChatSession).GetProperty("CreatedAt");
        createdAtProp?.SetValue(session, entity.CreatedAt);

        var lastMessageAtProp = typeof(ChatSession).GetProperty("LastMessageAt");
        lastMessageAtProp?.SetValue(session, entity.LastMessageAt);

        var isArchivedProp = typeof(ChatSession).GetProperty("IsArchived");
        isArchivedProp?.SetValue(session, entity.IsArchived);

        return session;
    }

    private static void AddMessageViaReflection(ChatSession session, SessionChatMessage message)
    {
#pragma warning disable S3011 // Reflection needed for domain reconstruction from persistence
        var messagesField = typeof(ChatSession).GetField("_messages",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var messages = messagesField?.GetValue(session) as List<SessionChatMessage>;
        messages?.Add(message);
#pragma warning restore S3011
    }

    private static ChatSessionEntity MapToEntity(ChatSession session)
    {
        var messageDtos = session.Messages.Select(m => new PersistenceMessageDto(
            Id: m.Id,
            Content: m.Content,
            Role: m.Role,
            Timestamp: m.Timestamp,
            SequenceNumber: m.SequenceNumber,
            MetadataJson: m.MetadataJson
        )).ToList();

        var messagesJson = JsonSerializer.Serialize(messageDtos, s_jsonOptions);

        return new ChatSessionEntity
        {
            Id = session.Id,
            UserId = session.UserId,
            GameId = session.GameId,
            UserLibraryEntryId = session.UserLibraryEntryId,
            AgentSessionId = session.AgentSessionId,
            AgentId = session.AgentId,
            AgentType = session.AgentType,
            AgentName = session.AgentName,
            Title = session.Title,
            AgentConfigJson = session.AgentConfigJson,
            CreatedAt = session.CreatedAt,
            LastMessageAt = session.LastMessageAt,
            IsArchived = session.IsArchived,
            MessagesJson = messagesJson
        };
    }

    // DTO for JSON serialization
    private sealed record PersistenceMessageDto(
        Guid Id,
        string Content,
        string Role,
        DateTime Timestamp,
        int SequenceNumber,
        string? MetadataJson = null
    );
}
