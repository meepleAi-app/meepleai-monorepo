using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

public class ChatThreadTests
{
    [Fact]
    public void ChatThread_Create_InitializesCorrectly()
    {
        // Arrange & Act
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var thread = new ChatThread(threadId, gameId, "My Thread");

        // Assert
        Assert.Equal(threadId, thread.Id);
        Assert.Equal(gameId, thread.GameId);
        Assert.Equal("My Thread", thread.Title);
        Assert.True(thread.IsEmpty);
        Assert.Equal(0, thread.MessageCount);
    }

    [Fact]
    public void ChatThread_AddUserMessage_AddsToMessages()
    {
        // Arrange
        var thread = new ChatThread(Guid.NewGuid());

        // Act
        thread.AddUserMessage("Hello");

        // Assert
        Assert.Equal(1, thread.MessageCount);
        Assert.False(thread.IsEmpty);
        Assert.Equal("Hello", thread.LastMessage?.Content);
        Assert.True(thread.LastMessage?.IsUserMessage);
    }

    [Fact]
    public void ChatThread_AddAssistantMessage_AddsToMessages()
    {
        // Arrange
        var thread = new ChatThread(Guid.NewGuid());

        // Act
        thread.AddAssistantMessage("Response");

        // Assert
        Assert.Equal(1, thread.MessageCount);
        Assert.Equal("Response", thread.LastMessage?.Content);
        Assert.True(thread.LastMessage?.IsAssistantMessage);
    }

    [Fact]
    public void ChatThread_AddMessage_UpdatesLastMessageAt()
    {
        // Arrange
        var thread = new ChatThread(Guid.NewGuid());
        var before = DateTime.UtcNow;

        // Act
        thread.AddUserMessage("Test");

        // Assert
        Assert.InRange(thread.LastMessageAt, before, DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void ChatThread_SetTitle_UpdatesTitle()
    {
        // Arrange
        var thread = new ChatThread(Guid.NewGuid());

        // Act
        thread.SetTitle("New Title");

        // Assert
        Assert.Equal("New Title", thread.Title);
    }

    [Fact]
    public void ChatThread_SetTitle_WithEmptyString_ThrowsArgumentException()
    {
        // Arrange
        var thread = new ChatThread(Guid.NewGuid());

        // Act & Assert
        Assert.Throws<ArgumentException>(() => thread.SetTitle(""));
        Assert.Throws<ArgumentException>(() => thread.SetTitle("   "));
    }

    [Fact]
    public void ChatThread_SetTitle_ExceedingMaxLength_ThrowsArgumentException()
    {
        // Arrange
        var thread = new ChatThread(Guid.NewGuid());
        var longTitle = new string('a', 201);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => thread.SetTitle(longTitle));
        Assert.Contains("cannot exceed 200 characters", exception.Message);
    }
}
