using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

[Trait("Category", TestCategories.Unit)]

public class ChatMessageTests
{
    [Fact]
    public void ChatMessage_WithValidContent_CreatesSuccessfully()
    {
        // Arrange & Act
        var message = new ChatMessage("Hello", ChatMessage.UserRole, sequenceNumber: 1);

        // Assert
        Assert.Equal("Hello", message.Content);
        Assert.Equal(ChatMessage.UserRole, message.Role);
        Assert.Equal(1, message.SequenceNumber);
        Assert.True(message.IsUserMessage);
        Assert.False(message.IsAssistantMessage);
    }

    [Fact]
    public void ChatMessage_AssistantRole_SetsCorrectly()
    {
        // Arrange & Act
        var message = new ChatMessage("Response", ChatMessage.AssistantRole, sequenceNumber: 2);

        // Assert
        Assert.Equal(ChatMessage.AssistantRole, message.Role);
        Assert.Equal(2, message.SequenceNumber);
        Assert.True(message.IsAssistantMessage);
        Assert.False(message.IsUserMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ChatMessage_WithEmptyContent_ThrowsValidationException(string invalidContent)
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            new ChatMessage(invalidContent, ChatMessage.UserRole, sequenceNumber: 1));
        Assert.Contains("content cannot be empty", exception.Message);
    }

    [Fact]
    public void ChatMessage_ExceedingMaxLength_ThrowsValidationException()
    {
        // Arrange
        var longContent = new string('a', 10001);

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            new ChatMessage(longContent, ChatMessage.UserRole, sequenceNumber: 1));
        Assert.Contains("cannot exceed 10,000 characters", exception.Message);
    }

    [Fact]
    public void ChatMessage_WithInvalidRole_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            new ChatMessage("Content", "invalid_role", sequenceNumber: 1));
        Assert.Contains("must be 'user' or 'assistant'", exception.Message);
    }

    [Fact]
    public void ChatMessage_TrimsWhitespace()
    {
        // Arrange & Act
        var message = new ChatMessage("  Content  ", ChatMessage.UserRole, sequenceNumber: 1);

        // Assert
        Assert.Equal("Content", message.Content);
    }

    [Fact]
    public void ChatMessage_SetsTimestamp()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var message = new ChatMessage("Test", ChatMessage.UserRole, sequenceNumber: 1);

        // Assert
        var after = DateTime.UtcNow;
        Assert.InRange(message.Timestamp, before, after);
    }

    [Fact]
    public void UpdateContent_UserMessage_UpdatesSuccessfully()
    {
        // Arrange
        var message = new ChatMessage("Original", ChatMessage.UserRole, sequenceNumber: 1);

        // Act
        message.UpdateContent("Updated content");

        // Assert
        Assert.Equal("Updated content", message.Content);
        Assert.NotNull(message.UpdatedAt);
    }

    [Fact]
    public void UpdateContent_AssistantMessage_ThrowsInvalidOperationException()
    {
        // Arrange
        var message = new ChatMessage("Response", ChatMessage.AssistantRole, sequenceNumber: 1);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            message.UpdateContent("New content"));
        Assert.Contains("Only user messages can be edited", exception.Message);
    }

    [Fact]
    public void UpdateContent_DeletedMessage_ThrowsInvalidOperationException()
    {
        // Arrange
        var message = new ChatMessage("Original", ChatMessage.UserRole, sequenceNumber: 1);
        message.Delete(Guid.NewGuid());

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            message.UpdateContent("New content"));
        Assert.Contains("Cannot edit deleted message", exception.Message);
    }

    [Fact]
    public void Delete_Message_SoftDeletesSuccessfully()
    {
        // Arrange
        var message = new ChatMessage("Test", ChatMessage.UserRole, sequenceNumber: 1);
        var userId = Guid.NewGuid();

        // Act
        message.Delete(userId);

        // Assert
        Assert.True(message.IsDeleted);
        Assert.NotNull(message.DeletedAt);
        Assert.Equal(userId, message.DeletedByUserId);
    }

    [Fact]
    public void Delete_AlreadyDeletedMessage_IsIdempotent()
    {
        // Arrange
        var message = new ChatMessage("Test", ChatMessage.UserRole, sequenceNumber: 1);
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        message.Delete(userId1);

        // Act
        message.Delete(userId2);

        // Assert
        Assert.True(message.IsDeleted);
        Assert.Equal(userId1, message.DeletedByUserId);
    }

    [Fact]
    public void Invalidate_AssistantMessage_MarksAsInvalidated()
    {
        // Arrange
        var message = new ChatMessage("Response", ChatMessage.AssistantRole, sequenceNumber: 1);

        // Act
        message.Invalidate();

        // Assert
        Assert.True(message.IsInvalidated);
    }

    [Fact]
    public void Invalidate_UserMessage_ThrowsInvalidOperationException()
    {
        // Arrange
        var message = new ChatMessage("Test", ChatMessage.UserRole, sequenceNumber: 1);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            message.Invalidate());
        Assert.Contains("Only assistant messages can be invalidated", exception.Message);
    }

    [Fact]
    public void ToString_TruncatesLongContent()
    {
        // Arrange
        var longContent = new string('a', 100);
        var message = new ChatMessage(longContent, ChatMessage.UserRole, sequenceNumber: 1);

        // Act
        var result = message.ToString();

        // Assert
        Assert.Contains("[user]", result);
        Assert.True(result.Length < longContent.Length + 20);
    }
}

