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
internal class ChatThreadRepository : RepositoryBase, IChatThreadRepository
{
    public ChatThreadRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    // CA1869: Cache JsonSerializerOptions for better performance
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new LegacyPersistenceChatMessageDtoConverter() }
    };

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

    public async Task<int> CountByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext.ChatThreads
            .AsNoTracking()
            .CountAsync(t => t.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
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

    public async Task<(IReadOnlyList<ChatThread> Items, int TotalCount)> FindByUserIdFilteredAsync(
        Guid userId,
        Guid? gameId = null,
        string? agentType = null,
        string? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.ChatThreads
            .AsNoTracking()
            .Where(t => t.UserId == userId);

        // Apply filters
        if (gameId.HasValue)
            query = query.Where(t => t.GameId == gameId.Value);

        if (!string.IsNullOrWhiteSpace(agentType))
            query = query.Where(t => t.AgentType == agentType);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(t => t.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Title != null && t.Title.Contains(search));

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // Apply ordering and pagination
        var threadEntities = await query
            .OrderByDescending(t => t.LastMessageAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var items = threadEntities.Select(MapToDomain).ToList();
        return (items, totalCount);
    }

    /// <summary>
    /// Admin-only: gets all chat threads across users with optional filtering and pagination.
    /// Issue #4917: Admin chat history real data.
    /// </summary>
    public async Task<(IReadOnlyList<IChatThreadRepository.AdminChatSummary> Items, int TotalCount)> GetAllFilteredAsync(
        string? agentType = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.ChatThreads
            .AsNoTracking()
            .Include(t => t.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(agentType))
            query = query.Where(t => t.AgentType == agentType);

        if (dateFrom.HasValue)
            query = query.Where(t => t.LastMessageAt >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(t => t.LastMessageAt <= dateTo.Value);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var entities = await query
            .OrderByDescending(t => t.LastMessageAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Lightweight projection — avoids full MapToDomain + MessagesJson deserialization per row.
        // Only the first 2 messages are parsed for the admin preview (Issue #4917 code review fix).
        var items = entities
            .Select(e =>
            {
                var (msgCount, preview) = ParseMessagesForAdmin(e.MessagesJson, maxPreview: 2);
                return new IChatThreadRepository.AdminChatSummary(
                    Id: e.Id,
                    UserId: e.UserId,
                    AgentType: e.AgentType,
                    MessageCount: msgCount,
                    LastMessageAt: e.LastMessageAt,
                    CreatedAt: e.CreatedAt,
                    UserEmail: e.User?.Email,
                    UserDisplayName: e.User?.DisplayName,
                    PreviewMessages: preview
                );
            })
            .ToList();

        return (items, totalCount);
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
    /// Lightweight JSON parse: returns total message count + first N messages for admin preview.
    /// Single-pass over MessagesJson — avoids full MapToDomain overhead for admin listing.
    /// </summary>
    private static (int Count, IReadOnlyList<(string Role, string Content)> Preview)
        ParseMessagesForAdmin(string messagesJson, int maxPreview)
    {
        if (string.IsNullOrWhiteSpace(messagesJson)) return (0, []);
        try
        {
            using var doc = JsonDocument.Parse(messagesJson);
            var all = doc.RootElement.EnumerateArray().ToList();
            var preview = all
                .Take(maxPreview)
                .Select(el =>
                {
                    var role = el.TryGetProperty("Role", out var r) ? r.GetString() ?? "" : "";
                    var content = el.TryGetProperty("Content", out var c) ? c.GetString() ?? "" : "";
                    return (role, content);
                })
                .ToList<(string Role, string Content)>();
            return (all.Count, preview);
        }
        catch (JsonException)
        {
            return (0, []);
        }
    }

    /// <summary>
    /// Maps persistence entity to domain entity.
    /// Handles legacy data migration for messages without Id/SequenceNumber (Issue #1215).
    /// </summary>
    private static ChatThread MapToDomain(Api.Infrastructure.Entities.ChatThreadEntity entity)
    {
        // Deserialize messages from JSON using flexible DTO that handles both legacy and modern formats
        var messageDtos = JsonSerializer.Deserialize<List<PersistenceChatMessageDto>>(entity.MessagesJson, s_jsonOptions)
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

            // Issue #4362: Hydrate agent-specific metadata
            if (dto.AgentType != null || dto.Confidence.HasValue || dto.CitationsJson != null || dto.TokenCount.HasValue)
            {
                typeof(ChatMessage).GetProperty(nameof(ChatMessage.AgentType))?.SetValue(message, dto.AgentType);
                typeof(ChatMessage).GetProperty(nameof(ChatMessage.Confidence))?.SetValue(message, dto.Confidence);
                typeof(ChatMessage).GetProperty(nameof(ChatMessage.CitationsJson))?.SetValue(message, dto.CitationsJson);
                typeof(ChatMessage).GetProperty(nameof(ChatMessage.TokenCount))?.SetValue(message, dto.TokenCount);
            }

            return message;
        }).ToList();

        // Create thread
        var thread = new ChatThread(
            id: entity.Id,
            userId: entity.UserId,
            gameId: entity.GameId,
            title: entity.Title,
            agentId: entity.AgentId, // Issue #2030
            agentType: entity.AgentType // Issue #4362
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

        // Issue #5259: Hydrate conversation summary and summarization tracking
        if (!string.IsNullOrEmpty(entity.ConversationSummary))
        {
            var summaryProp = typeof(ChatThread).GetProperty("ConversationSummary");
            summaryProp?.SetValue(thread, entity.ConversationSummary);
        }

        if (entity.LastSummarizedMessageCount > 0)
        {
            var lastSummarizedProp = typeof(ChatThread).GetProperty("LastSummarizedMessageCount");
            lastSummarizedProp?.SetValue(thread, entity.LastSummarizedMessageCount);
        }

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
            IsInvalidated: m.IsInvalidated,
            AgentType: m.AgentType,
            Confidence: m.Confidence,
            CitationsJson: m.CitationsJson,
            TokenCount: m.TokenCount
        )).ToList();

        var messagesJson = JsonSerializer.Serialize(messageDtos);

        return new Api.Infrastructure.Entities.ChatThreadEntity
        {
            Id = domainEntity.Id,
            UserId = domainEntity.UserId,
            GameId = domainEntity.GameId,
            AgentId = domainEntity.AgentId, // Issue #2030
            AgentType = domainEntity.AgentType, // Issue #4362
            Title = domainEntity.Title,
            Status = domainEntity.Status.Value,
            CreatedAt = domainEntity.CreatedAt,
            LastMessageAt = domainEntity.LastMessageAt,
            MessagesJson = messagesJson,
            ConversationSummary = domainEntity.ConversationSummary, // Issue #5259
            LastSummarizedMessageCount = domainEntity.LastSummarizedMessageCount
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
        bool IsInvalidated = false,
        // Issue #4362: Agent-specific metadata
        string? AgentType = null,
        float? Confidence = null,
        string? CitationsJson = null,
        int? TokenCount = null
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

            // Issue #4362: Agent-specific metadata (optional)
            var agentType = root.TryGetProperty("AgentType", out var agentTypeProp) && agentTypeProp.ValueKind != JsonValueKind.Null
                ? agentTypeProp.GetString()
                : null;

            var confidence = root.TryGetProperty("Confidence", out var confidenceProp) && confidenceProp.ValueKind != JsonValueKind.Null
                ? confidenceProp.GetSingle()
                : (float?)null;

            var citationsJson = root.TryGetProperty("CitationsJson", out var citationsProp) && citationsProp.ValueKind != JsonValueKind.Null
                ? citationsProp.GetString()
                : null;

            var tokenCount = root.TryGetProperty("TokenCount", out var tokenCountProp) && tokenCountProp.ValueKind != JsonValueKind.Null
                ? tokenCountProp.GetInt32()
                : (int?)null;

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
                IsInvalidated: isInvalidated,
                AgentType: agentType,
                Confidence: confidence,
                CitationsJson: citationsJson,
                TokenCount: tokenCount
            );
        }

        public override void Write(Utf8JsonWriter writer, PersistenceChatMessageDto value, JsonSerializerOptions options)
        {
            // Use default serialization for writing
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
