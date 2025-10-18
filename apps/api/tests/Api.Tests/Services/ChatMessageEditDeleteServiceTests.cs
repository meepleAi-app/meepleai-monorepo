using System;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Api.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;

namespace Api.Tests.Services;

/// <summary>
/// Unit tests for ChatService message editing and deletion functionality (CHAT-06).
///
/// Feature: Message editing and deletion
/// As a user
/// I want to edit and delete my chat messages
/// So that I can correct mistakes and maintain conversation accuracy
/// </summary>
public class ChatMessageEditDeleteServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _context;
    private readonly ChatService _chatService;
    private readonly AuditService _auditService;

    public ChatMessageEditDeleteServiceTests()
    {
        // Setup SQLite in-memory database
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        // Enable foreign keys for SQLite (required for referential integrity)
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new MeepleAiDbContext(options);
        _context.Database.EnsureCreated();

        // Create services
        _auditService = new AuditService(_context, NullLogger<AuditService>.Instance);
        _chatService = new ChatService(_context, NullLogger<ChatService>.Instance, _auditService);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// Helper method to seed test database with user, game, agent, chat, and message.
    /// Reduces duplication across tests.
    /// </summary>
    private async Task<(string userId, Guid chatId, Guid messageId)> SeedUserChatAndMessage()
    {
        // Create user
        var user = new UserEntity
        {
            Id = Guid.NewGuid().ToString(),
            Email = "test@example.com",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        // Create game
        var game = new GameEntity
        {
            Id = "test-game",
            Name = "Test Game",
            CreatedAt = DateTime.UtcNow
        };
        _context.Games.Add(game);

        // Create agent
        var agent = new AgentEntity
        {
            Id = "test-agent",
            GameId = "test-game",
            Name = "Test Agent",
            Kind = "qa",
            CreatedAt = DateTime.UtcNow
        };
        _context.Agents.Add(agent);

        // Create chat
        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            GameId = "test-game",
            AgentId = "test-agent",
            StartedAt = DateTime.UtcNow
        };
        _context.Chats.Add(chat);

        // Create message
        var message = new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            UserId = user.Id,
            Level = "user",
            Message = "Original message",
            SequenceNumber = 0,
            CreatedAt = DateTime.UtcNow
        };
        _context.ChatLogs.Add(message);

        await _context.SaveChangesAsync();

        return (user.Id, chat.Id, message.Id);
    }

    #region UpdateMessageAsync Tests

    /// <summary>
    /// Scenario: User successfully edits their own message
    ///   Given a user has a message in a chat
    ///   When UpdateMessageAsync is called with new content
    ///   Then message content is updated, UpdatedAt is set, and message is returned
    /// </summary>
    [Fact]
    public async Task UpdateMessageAsync_ValidUserMessage_UpdatesSuccessfully()
    {
        // Given: User has a message in a chat
        var (userId, chatId, messageId) = await SeedUserChatAndMessage();

        // When: UpdateMessageAsync is called with new content
        var newContent = "Updated message content";
        var updatedMessage = await _chatService.UpdateMessageAsync(chatId, messageId, newContent, userId);

        // Then: Message content is updated, UpdatedAt is set
        Assert.NotNull(updatedMessage);
        Assert.Equal(newContent, updatedMessage.Message);
        Assert.NotNull(updatedMessage.UpdatedAt);
        Assert.True(updatedMessage.UpdatedAt > updatedMessage.CreatedAt);

        // Verify persistence
        var storedMessage = await _context.ChatLogs.FindAsync(messageId);
        Assert.NotNull(storedMessage);
        Assert.Equal(newContent, storedMessage!.Message);
        Assert.NotNull(storedMessage.UpdatedAt);
    }

    /// <summary>
    /// Scenario: Editing AI-generated message throws InvalidOperationException
    ///   Given a message with UserId == null (AI-generated)
    ///   When UpdateMessageAsync is called
    ///   Then InvalidOperationException is thrown with "AI-generated messages cannot be edited"
    /// </summary>
    [Fact]
    public async Task UpdateMessageAsync_AIMessage_ThrowsInvalidOperationException()
    {
        // Given: AI-generated message (UserId is null)
        var (userId, chatId, _) = await SeedUserChatAndMessage();

        // Create AI-generated message
        var aiMessage = new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
            UserId = null, // AI-generated
            Level = "assistant",
            Message = "AI response",
            SequenceNumber = 1,
            CreatedAt = DateTime.UtcNow
        };
        _context.ChatLogs.Add(aiMessage);
        await _context.SaveChangesAsync();

        // When: UpdateMessageAsync is called
        // Then: InvalidOperationException is thrown
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _chatService.UpdateMessageAsync(chatId, aiMessage.Id, "New content", userId));

        Assert.Contains("AI-generated messages cannot be edited", exception.Message);
    }

    /// <summary>
    /// Scenario: User cannot edit another user's message
    ///   Given a message owned by user A
    ///   When user B calls UpdateMessageAsync
    ///   Then UnauthorizedAccessException is thrown
    /// </summary>
    [Fact]
    public async Task UpdateMessageAsync_DifferentUser_ThrowsUnauthorizedAccessException()
    {
        // Given: Message owned by user A
        var (userIdA, chatId, messageId) = await SeedUserChatAndMessage();

        // Create user B
        var userB = new UserEntity
        {
            Id = Guid.NewGuid().ToString(),
            Email = "userb@example.com",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(userB);
        await _context.SaveChangesAsync();

        // When: User B tries to edit user A's message
        // Then: UnauthorizedAccessException is thrown
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _chatService.UpdateMessageAsync(chatId, messageId, "New content", userB.Id));

        Assert.Contains("You can only edit your own messages", exception.Message);
    }

    /// <summary>
    /// Scenario: Non-existent message throws KeyNotFoundException
    ///   Given an invalid messageId
    ///   When UpdateMessageAsync is called
    ///   Then KeyNotFoundException is thrown
    /// </summary>
    [Fact]
    public async Task UpdateMessageAsync_MessageNotFound_ThrowsKeyNotFoundException()
    {
        // Given: Valid user and chat, but invalid messageId
        var (userId, chatId, _) = await SeedUserChatAndMessage();
        var invalidMessageId = Guid.NewGuid();

        // When: UpdateMessageAsync is called with invalid messageId
        // Then: KeyNotFoundException is thrown
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _chatService.UpdateMessageAsync(chatId, invalidMessageId, "New content", userId));

        Assert.Contains($"Message {invalidMessageId} not found", exception.Message);
    }

    #endregion

    #region DeleteMessageAsync Tests

    /// <summary>
    /// Scenario: User successfully soft-deletes their own message
    ///   Given a user has a message in a chat
    ///   When DeleteMessageAsync is called
    ///   Then IsDeleted is true, DeletedAt is set, DeletedByUserId is set, returns true
    /// </summary>
    [Fact]
    public async Task DeleteMessageAsync_ValidUserMessage_SoftDeletesSuccessfully()
    {
        // Given: User has a message in a chat
        var (userId, chatId, messageId) = await SeedUserChatAndMessage();

        // When: DeleteMessageAsync is called
        var result = await _chatService.DeleteMessageAsync(chatId, messageId, userId, isAdmin: false);

        // Then: Message is soft-deleted
        Assert.True(result);

        // Verify message is soft-deleted (need IgnoreQueryFilters to see it)
        var deletedMessage = await _context.ChatLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == messageId);

        Assert.NotNull(deletedMessage);
        Assert.True(deletedMessage!.IsDeleted);
        Assert.NotNull(deletedMessage.DeletedAt);
        Assert.Equal(userId, deletedMessage.DeletedByUserId);
        Assert.True(deletedMessage.DeletedAt > deletedMessage.CreatedAt);
    }

    /// <summary>
    /// Scenario: Deleting already-deleted message is idempotent
    ///   Given a message that is already soft-deleted
    ///   When DeleteMessageAsync is called again
    ///   Then returns false, no additional changes
    /// </summary>
    [Fact]
    public async Task DeleteMessageAsync_AlreadyDeleted_ReturnsFalse()
    {
        // Given: Message is already soft-deleted
        var (userId, chatId, messageId) = await SeedUserChatAndMessage();

        // First deletion
        await _chatService.DeleteMessageAsync(chatId, messageId, userId, isAdmin: false);

        // When: DeleteMessageAsync is called again
        var result = await _chatService.DeleteMessageAsync(chatId, messageId, userId, isAdmin: false);

        // Then: Returns false (idempotent)
        Assert.False(result);
    }

    /// <summary>
    /// Scenario: Admin can delete any user's message
    ///   Given a message owned by user A
    ///   When user B (admin) calls DeleteMessageAsync with isAdmin=true
    ///   Then message is deleted, DeletedByUserId is user B, returns true
    /// </summary>
    [Fact]
    public async Task DeleteMessageAsync_AdminDeletesOtherUserMessage_Succeeds()
    {
        // Given: Message owned by user A
        var (userIdA, chatId, messageId) = await SeedUserChatAndMessage();

        // Create admin user B
        var adminUser = new UserEntity
        {
            Id = Guid.NewGuid().ToString(),
            Email = "admin@example.com",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(adminUser);
        await _context.SaveChangesAsync();

        // When: Admin user B deletes user A's message
        var result = await _chatService.DeleteMessageAsync(chatId, messageId, adminUser.Id, isAdmin: true);

        // Then: Message is deleted, DeletedByUserId is admin
        Assert.True(result);

        var deletedMessage = await _context.ChatLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == messageId);

        Assert.NotNull(deletedMessage);
        Assert.True(deletedMessage!.IsDeleted);
        Assert.Equal(adminUser.Id, deletedMessage.DeletedByUserId); // Admin's ID, not original owner
    }

    /// <summary>
    /// Scenario: Non-admin cannot delete another user's message
    ///   Given a message owned by user A
    ///   When user B (non-admin) calls DeleteMessageAsync
    ///   Then UnauthorizedAccessException is thrown
    /// </summary>
    [Fact]
    public async Task DeleteMessageAsync_NonAdminDeletesOtherUserMessage_ThrowsUnauthorizedAccessException()
    {
        // Given: Message owned by user A
        var (userIdA, chatId, messageId) = await SeedUserChatAndMessage();

        // Create non-admin user B
        var userB = new UserEntity
        {
            Id = Guid.NewGuid().ToString(),
            Email = "userb@example.com",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(userB);
        await _context.SaveChangesAsync();

        // When: Non-admin user B tries to delete user A's message
        // Then: UnauthorizedAccessException is thrown
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _chatService.DeleteMessageAsync(chatId, messageId, userB.Id, isAdmin: false));

        Assert.Contains("You can only delete your own messages", exception.Message);
    }

    /// <summary>
    /// Scenario: Non-existent message throws KeyNotFoundException
    ///   Given an invalid messageId
    ///   When DeleteMessageAsync is called
    ///   Then KeyNotFoundException is thrown
    /// </summary>
    [Fact]
    public async Task DeleteMessageAsync_MessageNotFound_ThrowsKeyNotFoundException()
    {
        // Given: Valid user and chat, but invalid messageId
        var (userId, chatId, _) = await SeedUserChatAndMessage();
        var invalidMessageId = Guid.NewGuid();

        // When: DeleteMessageAsync is called with invalid messageId
        // Then: KeyNotFoundException is thrown
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _chatService.DeleteMessageAsync(chatId, invalidMessageId, userId, isAdmin: false));

        Assert.Contains($"Message {invalidMessageId} not found", exception.Message);
    }

    #endregion

    #region InvalidateSubsequentMessagesAsync Tests

    /// <summary>
    /// Scenario: Invalidation marks subsequent AI messages
    ///   Given a chat with user message at seq 0, AI message at seq 1
    ///   When InvalidateSubsequentMessagesAsync(chatId, 0) is called
    ///   Then AI message at seq 1 has IsInvalidated = true, returns 1
    /// </summary>
    [Fact]
    public async Task InvalidateSubsequentMessagesAsync_WithSubsequentAIMessages_InvalidatesThem()
    {
        // Given: Chat with user message at seq 0, AI message at seq 1
        var (userId, chatId, userMessageId) = await SeedUserChatAndMessage();

        // Add AI response at sequence 1
        var aiMessage = new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
            UserId = null, // AI-generated
            Level = "assistant",
            Message = "AI response",
            SequenceNumber = 1,
            CreatedAt = DateTime.UtcNow
        };
        _context.ChatLogs.Add(aiMessage);
        await _context.SaveChangesAsync();

        // When: InvalidateSubsequentMessagesAsync is called from sequence 0
        var invalidatedCount = await _chatService.InvalidateSubsequentMessagesAsync(chatId, 0);

        // Then: AI message at seq 1 is invalidated, returns 1
        Assert.Equal(1, invalidatedCount);

        var invalidatedMessage = await _context.ChatLogs.FindAsync(aiMessage.Id);
        Assert.NotNull(invalidatedMessage);
        Assert.True(invalidatedMessage!.IsInvalidated);
    }

    /// <summary>
    /// Scenario: Invalidation does not affect user messages
    ///   Given a chat with user message at seq 0, AI message at seq 1, user message at seq 2
    ///   When InvalidateSubsequentMessagesAsync(chatId, 0) is called
    ///   Then AI message at seq 1 is invalidated, user message at seq 2 is NOT invalidated
    /// </summary>
    [Fact]
    public async Task InvalidateSubsequentMessagesAsync_IgnoresUserMessages()
    {
        // Given: Chat with user message at seq 0, AI message at seq 1, user message at seq 2
        var (userId, chatId, userMessage1Id) = await SeedUserChatAndMessage();

        // Add AI response at sequence 1
        var aiMessage = new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
            UserId = null, // AI-generated
            Level = "assistant",
            Message = "AI response",
            SequenceNumber = 1,
            CreatedAt = DateTime.UtcNow
        };
        _context.ChatLogs.Add(aiMessage);

        // Add user message at sequence 2
        var userMessage2 = new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
            UserId = userId,
            Level = "user",
            Message = "Follow-up question",
            SequenceNumber = 2,
            CreatedAt = DateTime.UtcNow
        };
        _context.ChatLogs.Add(userMessage2);
        await _context.SaveChangesAsync();

        // When: InvalidateSubsequentMessagesAsync is called from sequence 0
        var invalidatedCount = await _chatService.InvalidateSubsequentMessagesAsync(chatId, 0);

        // Then: Only AI message is invalidated (count = 1)
        Assert.Equal(1, invalidatedCount);

        var invalidatedAiMessage = await _context.ChatLogs.FindAsync(aiMessage.Id);
        Assert.True(invalidatedAiMessage!.IsInvalidated);

        var userMessage = await _context.ChatLogs.FindAsync(userMessage2.Id);
        Assert.False(userMessage!.IsInvalidated); // User message NOT invalidated
    }

    /// <summary>
    /// Scenario: No AI messages after sequence returns zero
    ///   Given a chat with no AI messages after sequence 5
    ///   When InvalidateSubsequentMessagesAsync(chatId, 5) is called
    ///   Then returns 0
    /// </summary>
    [Fact]
    public async Task InvalidateSubsequentMessagesAsync_NoAIMessagesAfterSequence_ReturnsZero()
    {
        // Given: Chat with only user message at sequence 0
        var (userId, chatId, messageId) = await SeedUserChatAndMessage();

        // When: InvalidateSubsequentMessagesAsync is called from sequence 5 (no messages after this)
        var invalidatedCount = await _chatService.InvalidateSubsequentMessagesAsync(chatId, 5);

        // Then: Returns 0 (no AI messages to invalidate)
        Assert.Equal(0, invalidatedCount);
    }

    /// <summary>
    /// Scenario: Already invalidated messages are not counted twice
    ///   Given a chat with AI message at seq 1 already invalidated
    ///   When InvalidateSubsequentMessagesAsync(chatId, 0) is called
    ///   Then returns 0 (no NEW messages invalidated)
    /// </summary>
    [Fact]
    public async Task InvalidateSubsequentMessagesAsync_AlreadyInvalidated_ReturnsZero()
    {
        // Given: Chat with AI message already invalidated
        var (userId, chatId, userMessageId) = await SeedUserChatAndMessage();

        // Add AI response at sequence 1, already invalidated
        var aiMessage = new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
            UserId = null, // AI-generated
            Level = "assistant",
            Message = "AI response",
            SequenceNumber = 1,
            IsInvalidated = true, // Already invalidated
            CreatedAt = DateTime.UtcNow
        };
        _context.ChatLogs.Add(aiMessage);
        await _context.SaveChangesAsync();

        // When: InvalidateSubsequentMessagesAsync is called from sequence 0
        var invalidatedCount = await _chatService.InvalidateSubsequentMessagesAsync(chatId, 0);

        // Then: Returns 0 (no NEW messages invalidated)
        Assert.Equal(0, invalidatedCount);
    }

    #endregion
}
