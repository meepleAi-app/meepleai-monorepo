using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class ChatService
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<ChatService> _logger;
    private readonly AuditService _auditService;

    public ChatService(MeepleAiDbContext db, ILogger<ChatService> logger, AuditService auditService)
    {
        _db = db;
        _logger = logger;
        _auditService = auditService;
    }

    public async Task<ChatEntity?> GetChatByIdAsync(Guid chatId, string userId, CancellationToken ct = default)
    {
        // PERF-03: Split query to avoid Include with OrderBy (causes client evaluation)
        var chat = await _db.Chats
            .Include(c => c.Game)
            .Include(c => c.Agent)
            .AsSplitQuery() // Optimize for multiple includes
            .FirstOrDefaultAsync(c => c.Id == chatId && c.UserId == userId, ct);

        if (chat == null)
        {
            return null;
        }

        // Load logs separately with proper ordering (executed server-side)
        await _db.Entry(chat)
            .Collection(c => c.Logs)
            .Query()
            .OrderBy(l => l.CreatedAt)
            .LoadAsync(ct);

        return chat;
    }

    public async Task<List<ChatEntity>> GetUserChatsAsync(string userId, int limit = 50, CancellationToken ct = default)
    {
        // PERF-03: Use AsSplitQuery to avoid cartesian explosion with multiple includes
        return await _db.Chats
            .Include(c => c.Game)
            .Include(c => c.Agent)
            .AsSplitQuery()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.LastMessageAt ?? c.StartedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<List<ChatEntity>> GetUserChatsByGameAsync(string userId, string gameId, int limit = 50, CancellationToken ct = default)
    {
        // PERF-03: Use AsSplitQuery to avoid cartesian explosion with multiple includes
        return await _db.Chats
            .Include(c => c.Game)
            .Include(c => c.Agent)
            .AsSplitQuery()
            .Where(c => c.UserId == userId && c.GameId == gameId)
            .OrderByDescending(c => c.LastMessageAt ?? c.StartedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<ChatEntity> CreateChatAsync(string userId, string gameId, string agentId, CancellationToken ct = default)
    {
        // Verify game exists
        var game = await _db.Games.FindAsync(new object[] { gameId }, ct);
        if (game == null)
        {
            throw new InvalidOperationException($"Game with ID '{gameId}' not found");
        }

        // Verify agent exists and belongs to the game
        var agent = await _db.Agents.FindAsync(new object[] { agentId }, ct);
        if (agent == null || agent.GameId != gameId)
        {
            throw new InvalidOperationException($"Agent with ID '{agentId}' not found for game '{gameId}'");
        }

        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = gameId,
            AgentId = agentId,
            StartedAt = DateTime.UtcNow,
            LastMessageAt = null
        };

        _db.Chats.Add(chat);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created chat {ChatId} for user {UserId}, game {GameId}, agent {AgentId}",
            chat.Id, userId, gameId, agentId);

        return chat;
    }

    public async Task<ChatLogEntity> AddMessageAsync(
        Guid chatId,
        string userId,
        string level,
        string message,
        object? metadata = null,
        CancellationToken ct = default)
    {
        var chat = await _db.Chats.FindAsync(new object[] { chatId }, ct);
        if (chat == null || chat.UserId != userId)
        {
            throw new InvalidOperationException($"Chat with ID '{chatId}' not found or access denied");
        }

        // CHAT-06: Calculate next sequence number for cascade invalidation
        var maxSequence = await _db.ChatLogs
            .Where(cl => cl.ChatId == chatId)
            .MaxAsync(cl => (int?)cl.SequenceNumber, ct) ?? 0;

        var chatLog = new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
            UserId = level == "user" ? userId : null, // CHAT-06: Only user messages have UserId
            SequenceNumber = maxSequence + 1, // CHAT-06: Auto-increment sequence
            Level = level,
            Message = message,
            MetadataJson = metadata != null ? JsonSerializer.Serialize(metadata) : null,
            CreatedAt = DateTime.UtcNow
        };

        _db.ChatLogs.Add(chatLog);

        // Update LastMessageAt on the chat
        chat.LastMessageAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Added message to chat {ChatId}, level: {Level}", chatId, level);

        return chatLog;
    }

    public async Task<bool> DeleteChatAsync(Guid chatId, string userId, CancellationToken ct = default)
    {
        var chat = await _db.Chats.FindAsync(new object[] { chatId }, ct);
        if (chat == null)
        {
            return false;
        }

        if (chat.UserId != userId)
        {
            throw new UnauthorizedAccessException($"User {userId} does not have permission to delete chat {chatId}");
        }

        _db.Chats.Remove(chat);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted chat {ChatId} for user {UserId}", chatId, userId);

        return true;
    }

    public async Task<List<ChatLogEntity>> GetChatHistoryAsync(Guid chatId, string userId, int limit = 100, CancellationToken ct = default)
    {
        var chat = await _db.Chats.FindAsync(new object[] { chatId }, ct);
        if (chat == null || chat.UserId != userId)
        {
            throw new InvalidOperationException($"Chat with ID '{chatId}' not found or access denied");
        }

        return await _db.ChatLogs
            .Where(l => l.ChatId == chatId)
            .OrderBy(l => l.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<List<AgentEntity>> GetAgentsForGameAsync(string gameId, CancellationToken ct = default)
    {
        return await _db.Agents
            .Where(a => a.GameId == gameId)
            .OrderBy(a => a.Name)
            .ToListAsync(ct);
    }

    public async Task<AgentEntity?> GetOrCreateAgentAsync(string gameId, string agentKind, CancellationToken ct = default)
    {
        // Try to find existing agent
        var agent = await _db.Agents
            .FirstOrDefaultAsync(a => a.GameId == gameId && a.Kind == agentKind, ct);

        if (agent != null)
        {
            return agent;
        }

        // Create new agent if it doesn't exist
        var agentId = $"{gameId}-{agentKind}";
        var agentName = agentKind switch
        {
            "qa" => "Q&A Agent",
            "explain" => "Explain Agent",
            "setup" => "Setup Guide Agent",
            _ => $"{agentKind} Agent"
        };

        agent = new AgentEntity
        {
            Id = agentId,
            GameId = gameId,
            Name = agentName,
            Kind = agentKind,
            CreatedAt = DateTime.UtcNow
        };

        _db.Agents.Add(agent);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created agent {AgentId} for game {GameId}, kind: {Kind}", agentId, gameId, agentKind);

        return agent;
    }

    /// <summary>
    /// Updates the content of an existing message.
    /// </summary>
    /// <param name="chatId">The chat ID containing the message</param>
    /// <param name="messageId">The message ID to edit</param>
    /// <param name="newContent">The new message content</param>
    /// <param name="userId">The user performing the edit (for authorization)</param>
    /// <returns>The updated message entity</returns>
    /// <exception cref="UnauthorizedAccessException">User does not own the message</exception>
    /// <exception cref="InvalidOperationException">Cannot edit AI-generated messages</exception>
    /// <exception cref="KeyNotFoundException">Message not found</exception>
    public async Task<ChatLogEntity> UpdateMessageAsync(Guid chatId, Guid messageId, string newContent, string userId, CancellationToken ct = default)
    {
        // Load message with EF tracking enabled
        var message = await _db.ChatLogs
            .FirstOrDefaultAsync(m => m.Id == messageId && m.ChatId == chatId, ct);

        if (message == null)
        {
            throw new KeyNotFoundException($"Message {messageId} not found in chat {chatId}");
        }

        // AI-generated messages cannot be edited
        if (message.UserId == null)
        {
            throw new InvalidOperationException("AI-generated messages cannot be edited");
        }

        // User can only edit their own messages
        if (message.UserId != userId)
        {
            throw new UnauthorizedAccessException("You can only edit your own messages");
        }

        // Store original content for audit log
        var originalContent = message.Message;

        // Update message content and timestamp
        message.Message = newContent;
        message.UpdatedAt = DateTime.UtcNow;

        // Invalidate subsequent AI responses
        await InvalidateSubsequentMessagesAsync(chatId, message.SequenceNumber, ct);

        // Save changes atomically
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Updated message {MessageId} in chat {ChatId} by user {UserId}",
            messageId, chatId, userId);

        // Log to audit service for compliance
        await _auditService.LogAsync(
            userId,
            "message_updated",
            "chat_message",
            messageId.ToString(),
            "Success",
            JsonSerializer.Serialize(new
            {
                chatId,
                messageId,
                originalContent,
                newContent,
                updatedAt = message.UpdatedAt
            }),
            ct: ct);

        return message;
    }

    /// <summary>
    /// Soft-deletes a message and invalidates subsequent AI responses.
    /// </summary>
    /// <param name="chatId">The chat ID containing the message</param>
    /// <param name="messageId">The message ID to delete</param>
    /// <param name="userId">The user performing the deletion (for authorization)</param>
    /// <param name="isAdmin">Whether the user has admin privileges</param>
    /// <returns>True if deleted, false if already deleted</returns>
    /// <exception cref="UnauthorizedAccessException">User does not own the message and is not admin</exception>
    /// <exception cref="KeyNotFoundException">Message not found</exception>
    public async Task<bool> DeleteMessageAsync(Guid chatId, Guid messageId, string userId, bool isAdmin = false, CancellationToken ct = default)
    {
        // Load message with IgnoreQueryFilters to include already soft-deleted messages
        var message = await _db.ChatLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == messageId && m.ChatId == chatId, ct);

        if (message == null)
        {
            throw new KeyNotFoundException($"Message {messageId} not found in chat {chatId}");
        }

        // Idempotent: return false if already deleted
        if (message.IsDeleted)
        {
            return false;
        }

        // Authorization: user must own the message OR be admin
        if (!isAdmin && message.UserId != userId)
        {
            throw new UnauthorizedAccessException("You can only delete your own messages");
        }

        // Store content for audit log before deletion
        var deletedContent = message.Message;

        // Soft delete the message
        message.IsDeleted = true;
        message.DeletedAt = DateTime.UtcNow;
        message.DeletedByUserId = userId;

        // Invalidate subsequent AI responses
        await InvalidateSubsequentMessagesAsync(chatId, message.SequenceNumber, ct);

        // Save changes atomically
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Deleted message {MessageId} in chat {ChatId} by user {UserId} (admin: {IsAdmin})",
            messageId, chatId, userId, isAdmin);

        // Log to audit service for compliance
        await _auditService.LogAsync(
            userId,
            "message_deleted",
            "chat_message",
            messageId.ToString(),
            "Success",
            JsonSerializer.Serialize(new
            {
                chatId,
                messageId,
                deletedContent,
                isAdminDelete = isAdmin,
                deletedAt = message.DeletedAt
            }),
            ct: ct);

        return true;
    }

    /// <summary>
    /// Marks all AI-generated messages after a given sequence number as invalidated.
    /// </summary>
    /// <param name="chatId">The chat ID</param>
    /// <param name="fromSequenceNumber">Start invalidating from this sequence number (exclusive)</param>
    /// <returns>Number of messages invalidated</returns>
    public async Task<int> InvalidateSubsequentMessagesAsync(Guid chatId, int fromSequenceNumber, CancellationToken ct = default)
    {
        // Use ExecuteUpdateAsync for performance (bulk update without loading entities)
        var invalidatedCount = await _db.ChatLogs
            .Where(m => m.ChatId == chatId
                && m.SequenceNumber > fromSequenceNumber
                && m.Level == "assistant"
                && !m.IsInvalidated)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(m => m.IsInvalidated, true),
                ct);

        if (invalidatedCount > 0)
        {
            _logger.LogDebug(
                "Invalidated {Count} AI messages in chat {ChatId} after sequence {SequenceNumber}",
                invalidatedCount, chatId, fromSequenceNumber);
        }

        return invalidatedCount;
    }
}
