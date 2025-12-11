using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of ChatThread repository.
/// Maps between domain ChatThread entity and ChatThreadEntity persistence model.
/// </summary>
public class ChatThreadRepository : RepositoryBase, IChatThreadRepository
{
    public ChatThreadRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<ChatThread?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var threadEntity = await DbContext.ChatThreads
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken).ConfigureAwait(false);

        return threadEntity != null ? MapToDomain(threadEntity) : null;
    }

    public async Task<IReadOnlyList<ChatThread>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var threadEntities = await DbContext.ChatThreads
            .AsNoTracking()
            .OrderByDescending(t => t.LastMessageAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return threadEntities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<ChatThread>> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var threadEntities = await DbContext.ChatThreads
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.LastMessageAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return threadEntities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<ChatThread>> FindByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var threadEntities = await DbContext.ChatThreads
            .AsNoTracking()
            .Where(t => t.GameId == gameId)
            .OrderByDescending(t => t.LastMessageAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return threadEntities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<ChatThread>> FindByUserIdAndGameIdAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        var threadEntities = await DbContext.ChatThreads
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.GameId == gameId)
            .OrderByDescending(t => t.LastMessageAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return threadEntities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<ChatThread>> GetRecentAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        var threadEntities = await DbContext.ChatThreads
            .AsNoTracking()
            .OrderByDescending(t => t.LastMessageAt)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return threadEntities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(ChatThread thread, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(thread);
        var threadEntity = MapToPersistence(thread);
        await DbContext.ChatThreads.AddAsync(threadEntity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(ChatThread thread, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(thread);
        var threadEntity = MapToPersistence(thread);

        // Detach existing tracked entity to avoid conflicts (SPRINT-5 Issue #1142)
        var tracked = DbContext.ChangeTracker.Entries<Api.Infrastructure.Entities.ChatThreadEntity>()
            .FirstOrDefault(e => e.Entity.Id == threadEntity.Id);

        if (tracked != null)
            tracked.State = EntityState.Detached;

        DbContext.ChatThreads.Update(threadEntity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ChatThread thread, CancellationToken cancellationToken = default)
    {
        var threadEntity = MapToPersistence(thread);
        DbContext.ChatThreads.Remove(threadEntity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.ChatThreads.AnyAsync(t => t.Id == id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Maps persistence entity to domain entity.
    /// Handles legacy data migration for messages without Id/SequenceNumber (Issue #1215).
    /// </summary>
    private static ChatThread MapToDomain(Api.Infrastructure.Entities.ChatThreadEntity entity)
    {
        // Deserialize messages from JSON using flexible DTO that handles both legacy and modern formats
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Converters = { new LegacyPersistenceChatMessageDtoConverter() }
        };

        var messageDtos = JsonSerializer.Deserialize<List<PersistenceChatMessageDto>>(entity.MessagesJson, jsonOptions)
            ?? new List<PersistenceChatMessageDto>();

        // ISSUE-1215: Generate stable fallback values for legacy messages and hydrate all fields
        var messages = messageDtos.Select((dto, index) =>
        {
            var message = new ChatMessage(
                content: dto.Content,
                role: dto.Role,
                sequenceNumber: dto.SequenceNumber > 0 ? dto.SequenceNumber : index + 1, // Fallback to index+1
                timestamp: dto.Timestamp,
                id: dto.Id != Guid.Empty ? dto.Id : GenerateStableGuid(entity.Id, dto.Content, dto.Timestamp, index) // Fallback to deterministic GUID
            );

            // Use reflection to hydrate additional fields from persistence (UpdatedAt, IsDeleted, etc.)
            if (dto.UpdatedAt.HasValue)
            {
                typeof(ChatMessage).GetProperty(nameof(ChatMessage.UpdatedAt))?.SetValue(message, dto.UpdatedAt);
            }

            if (dto.IsDeleted)
            {
                typeof(ChatMessage).GetProperty(nameof(ChatMessage.IsDeleted))?.SetValue(message, dto.IsDeleted);
                typeof(ChatMessage).GetProperty(nameof(ChatMessage.DeletedAt))?.SetValue(message, dto.DeletedAt);
                typeof(ChatMessage).GetProperty(nameof(ChatMessage.DeletedByUserId))?.SetValue(message, dto.DeletedByUserId);
            }

            if (dto.IsInvalidated)
            {
                typeof(ChatMessage).GetProperty(nameof(ChatMessage.IsInvalidated))?.SetValue(message, dto.IsInvalidated);
            }

            return message;
        }).ToList();

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
        // Serialize messages to JSON with all fields
        var messageDtos = domainEntity.Messages.Select(m => new PersistenceChatMessageDto(
            Id: m.Id,
            Content: m.Content,
            Role: m.Role,
            Timestamp: m.Timestamp,
            SequenceNumber: m.SequenceNumber,
            UpdatedAt: m.UpdatedAt,
            IsDeleted: m.IsDeleted,
            DeletedAt: m.DeletedAt,
            DeletedByUserId: m.DeletedByUserId,
            IsInvalidated: m.IsInvalidated
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

    /// <summary>
    /// Generates a deterministic GUID for legacy messages without IDs.
    /// Uses SHA-256 hash of thread ID + content + timestamp + index to ensure stability.
    /// Same inputs always produce the same GUID (Issue #1215).
    /// </summary>
    private static Guid GenerateStableGuid(Guid threadId, string content, DateTime timestamp, int index)
    {
        var input = $"{threadId}|{content}|{timestamp:O}|{index}";
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));

        // Use first 16 bytes of hash as GUID
        var guidBytes = new byte[16];
        Array.Copy(hash, 0, guidBytes, 0, 16);

        // Set version to 5 (SHA-1 name-based) and variant bits per RFC 4122
        guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x50); // Version 5
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80); // Variant bits

        return new Guid(guidBytes);
    }

    // DTO for JSON serialization (internal to repository)
    private sealed record PersistenceChatMessageDto(
        Guid Id,
        string Content,
        string Role,
        DateTime Timestamp,
        int SequenceNumber,
        DateTime? UpdatedAt = null,
        bool IsDeleted = false,
        DateTime? DeletedAt = null,
        Guid? DeletedByUserId = null,
        bool IsInvalidated = false
    );

    /// <summary>
    /// Custom JSON converter to handle legacy messages without Id/SequenceNumber fields.
    /// Allows deserialization of both old format (no Id/SequenceNumber) and new format.
    /// </summary>
    private sealed class LegacyPersistenceChatMessageDtoConverter : System.Text.Json.Serialization.JsonConverter<PersistenceChatMessageDto>
    {
        public override PersistenceChatMessageDto Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            // Required fields
            var content = root.GetProperty("Content").GetString() ?? string.Empty;
            var role = root.GetProperty("Role").GetString() ?? string.Empty;
            var timestamp = root.GetProperty("Timestamp").GetDateTime();

            // Optional fields with defaults for legacy data
            var id = root.TryGetProperty("Id", out var idProp) && idProp.ValueKind != JsonValueKind.Null
                ? idProp.GetGuid()
                : Guid.Empty; // Will be generated later

            var sequenceNumber = root.TryGetProperty("SequenceNumber", out var seqProp)
                ? seqProp.GetInt32()
                : 0; // Will be generated later

            var updatedAt = root.TryGetProperty("UpdatedAt", out var updatedAtProp) && updatedAtProp.ValueKind != JsonValueKind.Null
                ? updatedAtProp.GetDateTime()
                : (DateTime?)null;

            var isDeleted = root.TryGetProperty("IsDeleted", out var isDeletedProp) && isDeletedProp.GetBoolean();

            var deletedAt = root.TryGetProperty("DeletedAt", out var deletedAtProp) && deletedAtProp.ValueKind != JsonValueKind.Null
                ? deletedAtProp.GetDateTime()
                : (DateTime?)null;

            var deletedByUserId = root.TryGetProperty("DeletedByUserId", out var deletedByUserIdProp) && deletedByUserIdProp.ValueKind != JsonValueKind.Null
                ? deletedByUserIdProp.GetGuid()
                : (Guid?)null;

            var isInvalidated = root.TryGetProperty("IsInvalidated", out var isInvalidatedProp) && isInvalidatedProp.GetBoolean();

            return new PersistenceChatMessageDto(
                Id: id,
                Content: content,
                Role: role,
                Timestamp: timestamp,
                SequenceNumber: sequenceNumber,
                UpdatedAt: updatedAt,
                IsDeleted: isDeleted,
                DeletedAt: deletedAt,
                DeletedByUserId: deletedByUserId,
                IsInvalidated: isInvalidated
            );
        }

        public override void Write(Utf8JsonWriter writer, PersistenceChatMessageDto value, JsonSerializerOptions options)
        {
            // Use default serialization for writing
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
