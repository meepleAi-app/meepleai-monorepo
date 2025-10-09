using System;
using System.Linq;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Unit tests for ChatService following BDD-style testing patterns.
///
/// Feature: Chat session management
/// As a user
/// I want to manage chat sessions with AI agents
/// So that I can maintain conversation history and context
/// </summary>
public class ChatServiceTests
{
    private static MeepleAiDbContext CreateInMemoryContext()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        // Enable foreign keys for SQLite
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new MeepleAiDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Scenario: User creates a new chat session
    ///   Given a valid game and agent exist
    ///   When user creates a chat
    ///   Then chat is persisted with correct user association
    ///   And StartedAt timestamp is set
    ///   And LastMessageAt is initially null
    /// </summary>
    [Fact]
    public async Task CreateChatAsync_WhenValidInput_CreatesChat()
    {
        // Given: Valid game and agent exist
        await using var dbContext = CreateInMemoryContext();
        var user = new UserEntity { Id = "user-123", Email = "user@test.com", PasswordHash = "hash", Role = UserRole.User, CreatedAt = DateTime.UtcNow };
        var game = new GameEntity { Id = "catan", Name = "Catan" };
        var agent = new AgentEntity { Id = "catan-qa", GameId = "catan", Name = "Q&A Agent", Kind = "qa", CreatedAt = DateTime.UtcNow };
        dbContext.Users.Add(user);
        dbContext.Games.Add(game);
        dbContext.Agents.Add(agent);
        await dbContext.SaveChangesAsync();

        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance);

        // When: User creates a chat
        var chat = await service.CreateChatAsync("user-123", "catan", "catan-qa");

        // Then: Chat is persisted with correct associations
        Assert.NotEqual(Guid.Empty, chat.Id);
        Assert.Equal("user-123", chat.UserId);
        Assert.Equal("catan", chat.GameId);
        Assert.Equal("catan-qa", chat.AgentId);
        Assert.NotEqual(default, chat.StartedAt);
        Assert.Null(chat.LastMessageAt);

        var stored = await dbContext.Chats.FirstAsync();
        Assert.Equal(chat.Id, stored.Id);
    }

    /// <summary>
    /// Scenario: User tries to create chat with non-existent game
    ///   Given game does not exist
    ///   When user tries to create chat
    ///   Then InvalidOperationException is thrown
    /// </summary>
    [Fact]
    public async Task CreateChatAsync_WhenGameNotFound_Throws()
    {
        await using var dbContext = CreateInMemoryContext();
        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateChatAsync("user-123", "nonexistent", "agent-1"));
    }

    /// <summary>
    /// Scenario: User tries to create chat with non-existent agent
    ///   Given game exists but agent does not
    ///   When user tries to create chat
    ///   Then InvalidOperationException is thrown
    /// </summary>
    [Fact]
    public async Task CreateChatAsync_WhenAgentNotFound_Throws()
    {
        await using var dbContext = CreateInMemoryContext();
        var game = new GameEntity { Id = "catan", Name = "Catan" };
        dbContext.Games.Add(game);
        await dbContext.SaveChangesAsync();

        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateChatAsync("user-123", "catan", "nonexistent-agent"));
    }

    /// <summary>
    /// Scenario: User retrieves their own chat
    ///   Given user has created a chat
    ///   When user requests that chat by ID
    ///   Then chat is returned with full details including logs
    /// </summary>
    [Fact]
    public async Task GetChatByIdAsync_WhenOwner_ReturnsChat()
    {
        await using var dbContext = CreateInMemoryContext();
        var user = new UserEntity { Id = "user-123", Email = "user@test.com", PasswordHash = "hash", Role = UserRole.User, CreatedAt = DateTime.UtcNow };
        var game = new GameEntity { Id = "catan", Name = "Catan" };
        var agent = new AgentEntity { Id = "catan-qa", GameId = "catan", Name = "Q&A Agent", Kind = "qa", CreatedAt = DateTime.UtcNow };
        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = "user-123",
            GameId = "catan",
            AgentId = "catan-qa",
            StartedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        dbContext.Games.Add(game);
        dbContext.Agents.Add(agent);
        dbContext.Chats.Add(chat);
        await dbContext.SaveChangesAsync();

        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance);

        var result = await service.GetChatByIdAsync(chat.Id, "user-123");

        Assert.NotNull(result);
        Assert.Equal(chat.Id, result!.Id);
        Assert.Equal("user-123", result.UserId);
    }

    /// <summary>
    /// Scenario: User tries to access another user's chat
    ///   Given chat belongs to different user
    ///   When user requests that chat by ID
    ///   Then null is returned (access denied)
    /// </summary>
    [Fact]
    public async Task GetChatByIdAsync_WhenNotOwner_ReturnsNull()
    {
        await using var dbContext = CreateInMemoryContext();
        var user1 = new UserEntity { Id = "user-123", Email = "user-123@test.com", PasswordHash = "hash", Role = UserRole.User, CreatedAt = DateTime.UtcNow };
        var user2 = new UserEntity { Id = "user-456", Email = "user-456@test.com", PasswordHash = "hash", Role = UserRole.User, CreatedAt = DateTime.UtcNow };
        var game = new GameEntity { Id = "catan", Name = "Catan" };
        var agent = new AgentEntity { Id = "catan-qa", GameId = "catan", Name = "Q&A Agent", Kind = "qa", CreatedAt = DateTime.UtcNow };
        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = "user-123",
            GameId = "catan",
            AgentId = "catan-qa",
            StartedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user1);
        dbContext.Users.Add(user2);
        dbContext.Games.Add(game);
        dbContext.Agents.Add(agent);
        dbContext.Chats.Add(chat);
        await dbContext.SaveChangesAsync();

        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance);

        var result = await service.GetChatByIdAsync(chat.Id, "user-456");

        Assert.Null(result);
    }

    /// <summary>
    /// Scenario: User adds message to chat
    ///   Given user owns a chat
    ///   When user adds a message
    ///   Then message is persisted
    ///   And chat's LastMessageAt is updated
    /// </summary>
    [Fact]
    public async Task AddMessageAsync_WhenValidChat_AddsMessageAndUpdatesTimestamp()
    {
        await using var dbContext = CreateInMemoryContext();
        var user = new UserEntity { Id = "user-123", Email = "user-123@test.com", PasswordHash = "hash", Role = UserRole.User, CreatedAt = DateTime.UtcNow };
        var game = new GameEntity { Id = "catan", Name = "Catan" };
        var agent = new AgentEntity { Id = "catan-qa", GameId = "catan", Name = "Q&A Agent", Kind = "qa", CreatedAt = DateTime.UtcNow };
        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = "user-123",
            GameId = "catan",
            AgentId = "catan-qa",
            StartedAt = DateTime.UtcNow,
            LastMessageAt = null
        };
        dbContext.Users.Add(user);
        dbContext.Games.Add(game);
        dbContext.Agents.Add(agent);
        dbContext.Chats.Add(chat);
        await dbContext.SaveChangesAsync();

        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance);

        var message = await service.AddMessageAsync(
            chat.Id,
            "user-123",
            "user",
            "How do I setup the game?",
            new { source = "test" });

        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.Equal(chat.Id, message.ChatId);
        Assert.Equal("user", message.Level);
        Assert.Equal("How do I setup the game?", message.Message);
        Assert.NotNull(message.MetadataJson);

        // Verify LastMessageAt was updated
        var updatedChat = await dbContext.Chats.FindAsync(chat.Id);
        Assert.NotNull(updatedChat!.LastMessageAt);
    }

    /// <summary>
    /// Scenario: User deletes their own chat
    ///   Given user owns a chat
    ///   When user deletes the chat
    ///   Then chat is removed from database
    ///   And associated messages are cascade deleted
    /// </summary>
    [Fact]
    public async Task DeleteChatAsync_WhenOwner_DeletesChat()
    {
        await using var dbContext = CreateInMemoryContext();
        var user = new UserEntity { Id = "user-123", Email = "user-123@test.com", PasswordHash = "hash", Role = UserRole.User, CreatedAt = DateTime.UtcNow };
        var game = new GameEntity { Id = "catan", Name = "Catan" };
        var agent = new AgentEntity { Id = "catan-qa", GameId = "catan", Name = "Q&A Agent", Kind = "qa", CreatedAt = DateTime.UtcNow };
        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = "user-123",
            GameId = "catan",
            AgentId = "catan-qa",
            StartedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        dbContext.Games.Add(game);
        dbContext.Agents.Add(agent);
        dbContext.Chats.Add(chat);
        await dbContext.SaveChangesAsync();

        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance);

        var result = await service.DeleteChatAsync(chat.Id, "user-123");

        Assert.True(result);
        Assert.Null(await dbContext.Chats.FindAsync(chat.Id));
    }

    /// <summary>
    /// Scenario: User tries to delete another user's chat
    ///   Given chat belongs to different user
    ///   When user tries to delete the chat
    ///   Then UnauthorizedAccessException is thrown
    /// </summary>
    [Fact]
    public async Task DeleteChatAsync_WhenNotOwner_ThrowsUnauthorized()
    {
        await using var dbContext = CreateInMemoryContext();
        var user1 = new UserEntity { Id = "user-123", Email = "user-123@test.com", PasswordHash = "hash", Role = UserRole.User, CreatedAt = DateTime.UtcNow };
        var user2 = new UserEntity { Id = "user-456", Email = "user-456@test.com", PasswordHash = "hash", Role = UserRole.User, CreatedAt = DateTime.UtcNow };
        var game = new GameEntity { Id = "catan", Name = "Catan" };
        var agent = new AgentEntity { Id = "catan-qa", GameId = "catan", Name = "Q&A Agent", Kind = "qa", CreatedAt = DateTime.UtcNow };
        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = "user-123",
            GameId = "catan",
            AgentId = "catan-qa",
            StartedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user1);
        dbContext.Users.Add(user2);
        dbContext.Games.Add(game);
        dbContext.Agents.Add(agent);
        dbContext.Chats.Add(chat);
        await dbContext.SaveChangesAsync();

        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DeleteChatAsync(chat.Id, "user-456"));
    }

    /// <summary>
    /// Scenario: Get user's chats ordered by most recent
    ///   Given user has multiple chats
    ///   When user requests their chats
    ///   Then chats are returned ordered by LastMessageAt/StartedAt descending
    /// </summary>
    [Fact]
    public async Task GetUserChatsAsync_ReturnsChatsOrderedByRecency()
    {
        await using var dbContext = CreateInMemoryContext();
        var user = new UserEntity { Id = "user-123", Email = "user-123@test.com", PasswordHash = "hash", Role = UserRole.User, CreatedAt = DateTime.UtcNow };
        var game = new GameEntity { Id = "catan", Name = "Catan" };
        var agent = new AgentEntity { Id = "catan-qa", GameId = "catan", Name = "Q&A Agent", Kind = "qa", CreatedAt = DateTime.UtcNow };
        dbContext.Users.Add(user);
        dbContext.Games.Add(game);
        dbContext.Agents.Add(agent);

        var oldChat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = "user-123",
            GameId = "catan",
            AgentId = "catan-qa",
            StartedAt = DateTime.UtcNow.AddDays(-2),
            LastMessageAt = DateTime.UtcNow.AddDays(-1)
        };
        var recentChat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = "user-123",
            GameId = "catan",
            AgentId = "catan-qa",
            StartedAt = DateTime.UtcNow.AddHours(-1),
            LastMessageAt = DateTime.UtcNow
        };
        dbContext.Chats.AddRange(oldChat, recentChat);
        await dbContext.SaveChangesAsync();

        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance);

        var chats = await service.GetUserChatsAsync("user-123");

        Assert.Equal(2, chats.Count);
        Assert.Equal(recentChat.Id, chats[0].Id);
        Assert.Equal(oldChat.Id, chats[1].Id);
    }

    /// <summary>
    /// Scenario: Get or create agent for a game
    ///   Given game exists but agent does not
    ///   When GetOrCreateAgentAsync is called
    ///   Then agent is created with correct kind and naming
    /// </summary>
    [Fact]
    public async Task GetOrCreateAgentAsync_WhenAgentNotExists_CreatesAgent()
    {
        await using var dbContext = CreateInMemoryContext();
        var game = new GameEntity { Id = "catan", Name = "Catan" };
        dbContext.Games.Add(game);
        await dbContext.SaveChangesAsync();

        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance);

        var agent = await service.GetOrCreateAgentAsync("catan", "qa");

        Assert.NotNull(agent);
        Assert.Equal("catan-qa", agent!.Id);
        Assert.Equal("catan", agent.GameId);
        Assert.Equal("Q&A Agent", agent.Name);
        Assert.Equal("qa", agent.Kind);

        // Verify it was persisted
        var stored = await dbContext.Agents.FindAsync("catan-qa");
        Assert.NotNull(stored);
    }

    /// <summary>
    /// Scenario: Get or create agent when agent already exists
    ///   Given agent already exists
    ///   When GetOrCreateAgentAsync is called
    ///   Then existing agent is returned without creating duplicate
    /// </summary>
    [Fact]
    public async Task GetOrCreateAgentAsync_WhenAgentExists_ReturnsExisting()
    {
        await using var dbContext = CreateInMemoryContext();
        var game = new GameEntity { Id = "catan", Name = "Catan" };
        var existingAgent = new AgentEntity
        {
            Id = "catan-qa",
            GameId = "catan",
            Name = "Q&A Agent",
            Kind = "qa",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        dbContext.Games.Add(game);
        dbContext.Agents.Add(existingAgent);
        await dbContext.SaveChangesAsync();

        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance);

        var agent = await service.GetOrCreateAgentAsync("catan", "qa");

        Assert.NotNull(agent);
        Assert.Equal("catan-qa", agent!.Id);
        Assert.Equal(existingAgent.CreatedAt, agent.CreatedAt); // Same instance

        // Verify no duplicate was created
        Assert.Equal(1, await dbContext.Agents.CountAsync(a => a.GameId == "catan" && a.Kind == "qa"));
    }
}
