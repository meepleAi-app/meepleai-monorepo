using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Infrastructure.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Text.Json;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Tests for legacy chat message migration in ChatThreadRepository (Issue #1215).
/// Validates that messages without Id/SequenceNumber are correctly hydrated with stable fallback values.
/// </summary>
public class ChatThreadRepository_LegacyMigrationTests : IAsyncLifetime
{
    private MeepleAiDbContext? _dbContext;
    private ChatThreadRepository? _repository;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
        await _dbContext.Database.EnsureCreatedAsync();
        _repository = new ChatThreadRepository(_dbContext, new Mock<IDomainEventCollector>().Object);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.Database.EnsureDeletedAsync();
            await _dbContext.DisposeAsync();
        }
    }

    [Fact]
    public async Task MapToDomain_LegacyMessagesWithoutIds_GeneratesStableGuids()
    {
        // Arrange: Create legacy JSON without Id and SequenceNumber
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var legacyMessages = new[]
        {
            new { Content = "What are the rules?", Role = "user", Timestamp = DateTime.UtcNow.AddHours(-2) },
            new { Content = "The game has 3 phases...", Role = "assistant", Timestamp = DateTime.UtcNow.AddHours(-1) }
        };

        var messagesJson = JsonSerializer.Serialize(legacyMessages);

        var threadEntity = new ChatThreadEntity
        {
            Id = threadId,
            UserId = userId,
            GameId = gameId,
            Title = "Legacy Thread",
            Status = "active",
            CreatedAt = DateTime.UtcNow.AddHours(-3),
            LastMessageAt = DateTime.UtcNow.AddHours(-1),
            MessagesJson = messagesJson
        };

        _dbContext!.ChatThreads.Add(threadEntity);
        await _dbContext.SaveChangesAsync();

        // Act: Load thread via repository
        var thread1 = await _repository!.GetByIdAsync(threadId);
        var thread2 = await _repository.GetByIdAsync(threadId); // Load again to test stability

        // Assert: IDs are generated and stable
        Assert.NotNull(thread1);
        Assert.NotNull(thread2);
        Assert.Equal(2, thread1.Messages.Count);
        Assert.Equal(2, thread2.Messages.Count);

        // Message 1: User message
        Assert.NotEqual(Guid.Empty, thread1.Messages[0].Id);
        Assert.Equal(thread1.Messages[0].Id, thread2.Messages[0].Id); // Stable across loads
        Assert.Equal("What are the rules?", thread1.Messages[0].Content);
        Assert.Equal("user", thread1.Messages[0].Role);
        Assert.Equal(1, thread1.Messages[0].SequenceNumber); // Fallback to index+1

        // Message 2: Assistant message
        Assert.NotEqual(Guid.Empty, thread1.Messages[1].Id);
        Assert.Equal(thread1.Messages[1].Id, thread2.Messages[1].Id); // Stable across loads
        Assert.NotEqual(thread1.Messages[0].Id, thread1.Messages[1].Id); // Unique IDs
        Assert.Equal("The game has 3 phases...", thread1.Messages[1].Content);
        Assert.Equal("assistant", thread1.Messages[1].Role);
        Assert.Equal(2, thread1.Messages[1].SequenceNumber); // Fallback to index+1
    }

    [Fact]
    public async Task MapToDomain_ModernMessagesWithIds_PreservesOriginalValues()
    {
        // Arrange: Create modern JSON with Id and SequenceNumber
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var message1Id = Guid.NewGuid();
        var message2Id = Guid.NewGuid();

        var modernMessages = new[]
        {
            new
            {
                Id = message1Id,
                Content = "What are the rules?",
                Role = "user",
                Timestamp = DateTime.UtcNow.AddHours(-2),
                SequenceNumber = 10, // Non-default value
                UpdatedAt = (DateTime?)null,
                IsDeleted = false,
                DeletedAt = (DateTime?)null,
                DeletedByUserId = (Guid?)null,
                IsInvalidated = false
            },
            new
            {
                Id = message2Id,
                Content = "The game has 3 phases...",
                Role = "assistant",
                Timestamp = DateTime.UtcNow.AddHours(-1),
                SequenceNumber = 20, // Non-default value
                UpdatedAt = (DateTime?)null,
                IsDeleted = false,
                DeletedAt = (DateTime?)null,
                DeletedByUserId = (Guid?)null,
                IsInvalidated = false
            }
        };

        var messagesJson = JsonSerializer.Serialize(modernMessages);

        var threadEntity = new ChatThreadEntity
        {
            Id = threadId,
            UserId = userId,
            GameId = gameId,
            Title = "Modern Thread",
            Status = "active",
            CreatedAt = DateTime.UtcNow.AddHours(-3),
            LastMessageAt = DateTime.UtcNow.AddHours(-1),
            MessagesJson = messagesJson
        };

        _dbContext!.ChatThreads.Add(threadEntity);
        await _dbContext.SaveChangesAsync();

        // Act: Load thread via repository
        var thread = await _repository!.GetByIdAsync(threadId);

        // Assert: Original IDs and sequence numbers are preserved
        Assert.NotNull(thread);
        Assert.Equal(2, thread.Messages.Count);

        Assert.Equal(message1Id, thread.Messages[0].Id); // Original ID preserved
        Assert.Equal(10, thread.Messages[0].SequenceNumber); // Original sequence preserved

        Assert.Equal(message2Id, thread.Messages[1].Id); // Original ID preserved
        Assert.Equal(20, thread.Messages[1].SequenceNumber); // Original sequence preserved
    }

    [Fact]
    public async Task MapToDomain_MixedLegacyAndModern_HandlesCorrectly()
    {
        // Arrange: Mix of messages with and without IDs
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var modernMessageId = Guid.NewGuid();

        var mixedMessages = new[]
        {
            new
            {
                Id = (Guid?)null, // Legacy: No ID
                Content = "Legacy message",
                Role = "user",
                Timestamp = DateTime.UtcNow.AddHours(-3),
                SequenceNumber = 0, // Legacy: No sequence number
                UpdatedAt = (DateTime?)null,
                IsDeleted = false,
                DeletedAt = (DateTime?)null,
                DeletedByUserId = (Guid?)null,
                IsInvalidated = false
            },
            new
            {
                Id = (Guid?)modernMessageId, // Modern: Has ID
                Content = "Modern message",
                Role = "assistant",
                Timestamp = DateTime.UtcNow.AddHours(-2),
                SequenceNumber = 100, // Modern: Has sequence number
                UpdatedAt = (DateTime?)null,
                IsDeleted = false,
                DeletedAt = (DateTime?)null,
                DeletedByUserId = (Guid?)null,
                IsInvalidated = false
            },
            new
            {
                Id = (Guid?)null, // Legacy: No ID
                Content = "Another legacy message",
                Role = "user",
                Timestamp = DateTime.UtcNow.AddHours(-1),
                SequenceNumber = 0, // Legacy: No sequence number
                UpdatedAt = (DateTime?)null,
                IsDeleted = false,
                DeletedAt = (DateTime?)null,
                DeletedByUserId = (Guid?)null,
                IsInvalidated = false
            }
        };

        // Serialize with null handling
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        };
        var messagesJson = JsonSerializer.Serialize(mixedMessages, options);

        var threadEntity = new ChatThreadEntity
        {
            Id = threadId,
            UserId = userId,
            GameId = gameId,
            Title = "Mixed Thread",
            Status = "active",
            CreatedAt = DateTime.UtcNow.AddHours(-4),
            LastMessageAt = DateTime.UtcNow.AddHours(-1),
            MessagesJson = messagesJson
        };

        _dbContext!.ChatThreads.Add(threadEntity);
        await _dbContext.SaveChangesAsync();

        // Act
        var thread = await _repository!.GetByIdAsync(threadId);

        // Assert
        Assert.NotNull(thread);
        Assert.Equal(3, thread.Messages.Count);

        // Message 0: Legacy (generated ID and sequence)
        Assert.NotEqual(Guid.Empty, thread.Messages[0].Id);
        Assert.Equal(1, thread.Messages[0].SequenceNumber);
        Assert.Equal("Legacy message", thread.Messages[0].Content);

        // Message 1: Modern (preserved ID and sequence)
        Assert.Equal(modernMessageId, thread.Messages[1].Id);
        Assert.Equal(100, thread.Messages[1].SequenceNumber);
        Assert.Equal("Modern message", thread.Messages[1].Content);

        // Message 2: Legacy (generated ID and sequence)
        Assert.NotEqual(Guid.Empty, thread.Messages[2].Id);
        Assert.Equal(3, thread.Messages[2].SequenceNumber);
        Assert.Equal("Another legacy message", thread.Messages[2].Content);

        // Verify all IDs are unique
        var ids = thread.Messages.Select(m => m.Id).ToHashSet();
        Assert.Equal(3, ids.Count);
    }

    [Fact]
    public async Task GeneratedIds_AreDeterministic_ForSameContent()
    {
        // Arrange: Create two identical legacy threads
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        var legacyMessages = new[]
        {
            new { Content = "Identical message", Role = "user", Timestamp = timestamp }
        };

        var messagesJson = JsonSerializer.Serialize(legacyMessages);

        // Thread 1
        var threadId1 = Guid.NewGuid();
        var threadEntity1 = new ChatThreadEntity
        {
            Id = threadId1,
            UserId = userId,
            GameId = gameId,
            Title = "Thread 1",
            Status = "active",
            CreatedAt = timestamp,
            LastMessageAt = timestamp,
            MessagesJson = messagesJson
        };

        // Thread 2 (same content, different thread ID)
        var threadId2 = Guid.NewGuid();
        var threadEntity2 = new ChatThreadEntity
        {
            Id = threadId2,
            UserId = userId,
            GameId = gameId,
            Title = "Thread 2",
            Status = "active",
            CreatedAt = timestamp,
            LastMessageAt = timestamp,
            MessagesJson = messagesJson
        };

        _dbContext!.ChatThreads.AddRange(threadEntity1, threadEntity2);
        await _dbContext.SaveChangesAsync();

        // Act
        var thread1 = await _repository!.GetByIdAsync(threadId1);
        var thread2 = await _repository.GetByIdAsync(threadId2);

        // Assert: Same thread content produces same message IDs
        Assert.NotNull(thread1);
        Assert.NotNull(thread2);
        Assert.Single(thread1.Messages);
        Assert.Single(thread2.Messages);

        // IDs are deterministic based on thread ID + content + timestamp + index
        // Different thread IDs should produce different message IDs
        Assert.NotEqual(thread1.Messages[0].Id, thread2.Messages[0].Id);
    }

    [Fact]
    public async Task UpdateAndDelete_WorkOnLegacyMessages_WithGeneratedIds()
    {
        // Arrange: Create legacy thread
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var legacyMessages = new[]
        {
            new { Content = "User question", Role = "user", Timestamp = DateTime.UtcNow.AddHours(-2) },
            new { Content = "Assistant answer", Role = "assistant", Timestamp = DateTime.UtcNow.AddHours(-1) }
        };

        var messagesJson = JsonSerializer.Serialize(legacyMessages);

        var threadEntity = new ChatThreadEntity
        {
            Id = threadId,
            UserId = userId,
            GameId = gameId,
            Title = "Legacy Thread",
            Status = "active",
            CreatedAt = DateTime.UtcNow.AddHours(-3),
            LastMessageAt = DateTime.UtcNow.AddHours(-1),
            MessagesJson = messagesJson
        };

        _dbContext!.ChatThreads.Add(threadEntity);
        await _dbContext.SaveChangesAsync();

        // Act: Load, update, and save
        var thread = await _repository!.GetByIdAsync(threadId);
        Assert.NotNull(thread);

        var userMessage = thread.Messages[0];
        var generatedId = userMessage.Id;

        // Update user message content
        userMessage.UpdateContent("Updated user question");

        // Invalidate assistant message (simulating edit workflow)
        thread.Messages[1].Invalidate();

        // Save changes
        await _repository.UpdateAsync(thread);
        await _dbContext.SaveChangesAsync();

        // Assert: Changes persisted
        var reloadedThread = await _repository.GetByIdAsync(threadId);
        Assert.NotNull(reloadedThread);

        // Verify updated content
        var reloadedUserMessage = reloadedThread.Messages[0];
        Assert.Equal(generatedId, reloadedUserMessage.Id); // ID remains stable
        Assert.Equal("Updated user question", reloadedUserMessage.Content);
        Assert.NotNull(reloadedUserMessage.UpdatedAt);

        // Verify invalidation
        Assert.True(reloadedThread.Messages[1].IsInvalidated);
    }
}
