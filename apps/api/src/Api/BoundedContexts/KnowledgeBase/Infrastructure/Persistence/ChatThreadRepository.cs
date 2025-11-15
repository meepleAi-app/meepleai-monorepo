using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of ChatThread repository.
/// Maps between domain ChatThread entity and ChatThreadEntity persistence model.
/// </summary>
public class ChatThreadRepository : IChatThreadRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public ChatThreadRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ChatThread?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var threadEntity = await _dbContext.ChatThreads
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return threadEntity != null ? MapToDomain(threadEntity) : null;
    }

    public async Task<List<ChatThread>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var threadEntities = await _dbContext.ChatThreads
            .AsNoTracking()
            .OrderByDescending(t => t.LastMessageAt)
            .ToListAsync(cancellationToken);

        return threadEntities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<ChatThread>> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var threadEntities = await _dbContext.ChatThreads
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.LastMessageAt)
            .ToListAsync(cancellationToken);

        return threadEntities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<ChatThread>> FindByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var threadEntities = await _dbContext.ChatThreads
            .AsNoTracking()
            .Where(t => t.GameId == gameId)
            .OrderByDescending(t => t.LastMessageAt)
            .ToListAsync(cancellationToken);

        return threadEntities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<ChatThread>> FindByUserIdAndGameIdAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        var threadEntities = await _dbContext.ChatThreads
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.GameId == gameId)
            .OrderByDescending(t => t.LastMessageAt)
            .ToListAsync(cancellationToken);

        return threadEntities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<ChatThread>> GetRecentAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        var threadEntities = await _dbContext.ChatThreads
            .AsNoTracking()
            .OrderByDescending(t => t.LastMessageAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return threadEntities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(ChatThread thread, CancellationToken cancellationToken = default)
    {
        var threadEntity = MapToPersistence(thread);
        await _dbContext.ChatThreads.AddAsync(threadEntity, cancellationToken);
    }

    public Task UpdateAsync(ChatThread thread, CancellationToken cancellationToken = default)
    {
        var threadEntity = MapToPersistence(thread);

        // Detach existing tracked entity to avoid conflicts (SPRINT-5 Issue #1142)
        var tracked = _dbContext.ChangeTracker.Entries<Api.Infrastructure.Entities.ChatThreadEntity>()
            .FirstOrDefault(e => e.Entity.Id == threadEntity.Id);

        if (tracked != null)
            tracked.State = EntityState.Detached;

        _dbContext.ChatThreads.Update(threadEntity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ChatThread thread, CancellationToken cancellationToken = default)
    {
        var threadEntity = MapToPersistence(thread);
        _dbContext.ChatThreads.Remove(threadEntity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ChatThreads.AnyAsync(t => t.Id == id, cancellationToken);
    }

    /// <summary>
    /// Maps persistence entity to domain entity.
    /// </summary>
    private static ChatThread MapToDomain(Api.Infrastructure.Entities.ChatThreadEntity entity)
    {
        // Deserialize messages from JSON
        var messageDtos = JsonSerializer.Deserialize<List<ChatMessageDto>>(entity.MessagesJson) ?? new List<ChatMessageDto>();
        var messages = messageDtos.Select(dto => new ChatMessage(dto.Content, dto.Role, dto.Timestamp)).ToList();

        // Create thread
        var thread = new ChatThread(
            id: entity.Id,
            userId: entity.UserId,
            gameId: entity.GameId,
            title: entity.Title
        );

        // Add messages via domain method (but disable Status validation during hydration)
        // We need to restore messages before setting Status to allow closed threads to have messages
        var statusProp = typeof(ChatThread).GetProperty("Status");
        statusProp?.SetValue(thread, ThreadStatus.Active); // Temporarily set to active

        foreach (var message in messages)
        {
            thread.AddMessage(message);
        }

        // Override timestamps and status from DB
        var createdAtProp = typeof(ChatThread).GetProperty("CreatedAt");
        createdAtProp?.SetValue(thread, entity.CreatedAt);

        var lastMessageAtProp = typeof(ChatThread).GetProperty("LastMessageAt");
        lastMessageAtProp?.SetValue(thread, entity.LastMessageAt);

        statusProp?.SetValue(thread, ThreadStatus.From(entity.Status));

        return thread;
    }

    /// <summary>
    /// Maps domain entity to persistence entity.
    /// </summary>
    private static Api.Infrastructure.Entities.ChatThreadEntity MapToPersistence(ChatThread domainEntity)
    {
        // Serialize messages to JSON
        var messageDtos = domainEntity.Messages.Select(m => new ChatMessageDto(
            Content: m.Content,
            Role: m.Role,
            Timestamp: m.Timestamp
        )).ToList();

        var messagesJson = JsonSerializer.Serialize(messageDtos);

        return new Api.Infrastructure.Entities.ChatThreadEntity
        {
            Id = domainEntity.Id,
            UserId = domainEntity.UserId,
            GameId = domainEntity.GameId,
            Title = domainEntity.Title,
            Status = domainEntity.Status.Value,
            CreatedAt = domainEntity.CreatedAt,
            LastMessageAt = domainEntity.LastMessageAt,
            MessagesJson = messagesJson
        };
    }

    // Simple DTO for JSON serialization
    private record ChatMessageDto(string Content, string Role, DateTime Timestamp);
}
