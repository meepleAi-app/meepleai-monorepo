using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class ChatService
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<ChatService> _logger;

    public ChatService(MeepleAiDbContext db, ILogger<ChatService> logger)
    {
        _db = db;
        _logger = logger;
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

        var chatLog = new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
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
}
