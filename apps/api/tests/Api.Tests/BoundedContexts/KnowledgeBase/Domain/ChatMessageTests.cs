using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

public class ChatMessageTests
{
    [Fact]
    public void ChatMessage_WithValidContent_CreatesSuccessfully()
    {
        // Arrange & Act
        var message = new ChatMessage("Hello", ChatMessage.UserRole);

        // Assert
        Assert.Equal("Hello", message.Content);
        Assert.Equal(ChatMessage.UserRole, message.Role);
        Assert.True(message.IsUserMessage);
        Assert.False(message.IsAssistantMessage);
    }

    [Fact]
    public void ChatMessage_AssistantRole_SetsCorrectly()
    {
        // Arrange & Act
        var message = new ChatMessage("Response", ChatMessage.AssistantRole);

        // Assert
        Assert.Equal(ChatMessage.AssistantRole, message.Role);
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
            new ChatMessage(invalidContent, ChatMessage.UserRole));
        Assert.Contains("content cannot be empty", exception.Message);
    }

    [Fact]
    public void ChatMessage_ExceedingMaxLength_ThrowsValidationException()
    {
        // Arrange
        var longContent = new string('a', 10001);

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            new ChatMessage(longContent, ChatMessage.UserRole));
        Assert.Contains("cannot exceed 10,000 characters", exception.Message);
    }

    [Fact]
    public void ChatMessage_WithInvalidRole_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            new ChatMessage("Content", "invalid_role"));
        Assert.Contains("must be 'user' or 'assistant'", exception.Message);
    }

    [Fact]
    public void ChatMessage_TrimsWhitespace()
    {
        // Arrange & Act
        var message = new ChatMessage("  Content  ", ChatMessage.UserRole);

        // Assert
        Assert.Equal("Content", message.Content);
    }

    [Fact]
    public void ChatMessage_SetsTimestamp()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var message = new ChatMessage("Test", ChatMessage.UserRole);

        // Assert
        var after = DateTime.UtcNow;
        Assert.InRange(message.Timestamp, before, after);
    }
}
