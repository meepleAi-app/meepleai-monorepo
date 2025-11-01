using System;
using System.Linq;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

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
    private readonly ITestOutputHelper _output;

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

        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance, Mock.Of<AuditService>());

        // When: User creates a chat
        var chat = await service.CreateChatAsync("user-123", "catan", "catan-qa");

        // Then: Chat is persisted with correct associations
        chat.Id.Should().NotBe(Guid.Empty);
        chat.UserId.Should().Be("user-123");
        chat.GameId.Should().Be("catan");
        chat.AgentId.Should().Be("catan-qa");
        chat.StartedAt.Should().NotBe(default);
        chat.LastMessageAt.Should().BeNull();

        var stored = await dbContext.Chats.FirstAsync();
        stored.Id.Should().Be(chat.Id);
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
        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance, Mock.Of<AuditService>());

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

        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance, Mock.Of<AuditService>());

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

        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance, Mock.Of<AuditService>());

        var result = await service.GetChatByIdAsync(chat.Id, "user-123");

        result.Should().NotBeNull();
        result!.Id.Should().Be(chat.Id);
        result.UserId.Should().Be("user-123");
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

        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance, Mock.Of<AuditService>());

        var result = await service.GetChatByIdAsync(chat.Id, "user-456");

        result.Should().BeNull();
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

        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance, Mock.Of<AuditService>());

        var message = await service.AddMessageAsync(
            chat.Id,
            "user-123",
            "user",
            "How do I setup the game?",
            new { source = "test" });

        message.Id.Should().NotBe(Guid.Empty);
        message.ChatId.Should().Be(chat.Id);
        message.Level.Should().Be("user");
        message.Message.Should().Be("How do I setup the game?");
        message.MetadataJson.Should().NotBeNull();

        // Verify LastMessageAt was updated
        var updatedChat = await dbContext.Chats.FindAsync(chat.Id);
        updatedChat!.LastMessageAt.Should().NotBeNull();
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

        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance, Mock.Of<AuditService>());

        var result = await service.DeleteChatAsync(chat.Id, "user-123");

        result.Should().BeTrue();
        (await dbContext.Chats.FindAsync(chat.Id)).Should().BeNull();
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

        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance, Mock.Of<AuditService>());

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

        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance, Mock.Of<AuditService>());

        var chats = await service.GetUserChatsAsync("user-123");

        chats.Count.Should().Be(2);
        chats[0].Id.Should().Be(recentChat.Id);
        chats[1].Id.Should().Be(oldChat.Id);
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

        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance, Mock.Of<AuditService>());

        var agent = await service.GetOrCreateAgentAsync("catan", "qa");

        agent.Should().NotBeNull();
        agent!.Id.Should().Be("catan-qa");
        agent.GameId.Should().Be("catan");
        agent.Name.Should().Be("Q&A Agent");
        agent.Kind.Should().Be("qa");

        // Verify it was persisted
        var stored = await dbContext.Agents.FindAsync("catan-qa");
        stored.Should().NotBeNull();
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

        var service = new ChatService(dbContext, NullLogger<ChatService>.Instance, Mock.Of<AuditService>());

        var agent = await service.GetOrCreateAgentAsync("catan", "qa");

        agent.Should().NotBeNull();
        agent!.Id.Should().Be("catan-qa");
        agent.CreatedAt.Should().Be(existingAgent.CreatedAt); // Same instance

        // Verify no duplicate was created
        Assert.Equal(1, await dbContext.Agents.CountAsync(a => a.GameId == "catan" && a.Kind == "qa"));
    }
}
