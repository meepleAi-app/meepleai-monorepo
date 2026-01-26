using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Tests for the ChatMessage entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 17
/// </summary>
[Trait("Category", "Unit")]
public sealed class ChatMessageTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidUserMessage_CreatesMessage()
    {
        // Arrange
        var content = "Hello, world!";
        var role = ChatMessage.UserRole;
        var sequenceNumber = 0;

        // Act
        var message = new ChatMessage(content, role, sequenceNumber);

        // Assert
        message.Content.Should().Be(content);
        message.Role.Should().Be(role);
        message.SequenceNumber.Should().Be(sequenceNumber);
        message.IsDeleted.Should().BeFalse();
        message.IsInvalidated.Should().BeFalse();
        message.IsUserMessage.Should().BeTrue();
        message.IsAssistantMessage.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithValidAssistantMessage_CreatesMessage()
    {
        // Arrange
        var content = "I can help with that!";
        var role = ChatMessage.AssistantRole;
        var sequenceNumber = 1;

        // Act
        var message = new ChatMessage(content, role, sequenceNumber);

        // Assert
        message.Content.Should().Be(content);
        message.Role.Should().Be(role);
        message.IsUserMessage.Should().BeFalse();
        message.IsAssistantMessage.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithCustomTimestamp_SetsTimestamp()
    {
        // Arrange
        var customTimestamp = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var message = new ChatMessage("Content", ChatMessage.UserRole, 0, customTimestamp);

        // Assert
        message.Timestamp.Should().Be(customTimestamp);
    }

    [Fact]
    public void Constructor_WithCustomId_SetsId()
    {
        // Arrange
        var customId = Guid.NewGuid();

        // Act
        var message = new ChatMessage("Content", ChatMessage.UserRole, 0, id: customId);

        // Assert
        message.Id.Should().Be(customId);
    }

    [Fact]
    public void Constructor_WithoutCustomId_GeneratesNewId()
    {
        // Act
        var message = new ChatMessage("Content", ChatMessage.UserRole, 0);

        // Assert
        message.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Constructor_WithEmptyContent_ThrowsValidationException()
    {
        // Act
        var action = () => new ChatMessage("", ChatMessage.UserRole, 0);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Message content cannot be empty*");
    }

    [Fact]
    public void Constructor_WithWhitespaceContent_ThrowsValidationException()
    {
        // Act
        var action = () => new ChatMessage("   ", ChatMessage.UserRole, 0);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Message content cannot be empty*");
    }

    [Fact]
    public void Constructor_WithContentExceeding10000Characters_ThrowsValidationException()
    {
        // Arrange
        var longContent = new string('a', 10001);

        // Act
        var action = () => new ChatMessage(longContent, ChatMessage.UserRole, 0);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Message content cannot exceed 10,000 characters*");
    }

    [Fact]
    public void Constructor_WithContentExactly10000Characters_Succeeds()
    {
        // Arrange
        var maxContent = new string('a', 10000);

        // Act
        var message = new ChatMessage(maxContent, ChatMessage.UserRole, 0);

        // Assert
        message.Content.Should().HaveLength(10000);
    }

    [Fact]
    public void Constructor_WithInvalidRole_ThrowsValidationException()
    {
        // Act
        var action = () => new ChatMessage("Content", "invalid_role", 0);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Role must be 'user' or 'assistant'*");
    }

    [Fact]
    public void Constructor_TrimsWhitespaceFromContent()
    {
        // Act
        var message = new ChatMessage("  Trimmed content  ", ChatMessage.UserRole, 0);

        // Assert
        message.Content.Should().Be("Trimmed content");
    }

    #endregion

    #region UpdateContent Tests

    [Fact]
    public void UpdateContent_WithValidContent_UpdatesContent()
    {
        // Arrange
        var message = new ChatMessage("Original", ChatMessage.UserRole, 0);

        // Act
        message.UpdateContent("Updated");

        // Assert
        message.Content.Should().Be("Updated");
        message.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateContent_OnAssistantMessage_ThrowsInvalidOperationException()
    {
        // Arrange
        var message = new ChatMessage("Content", ChatMessage.AssistantRole, 0);

        // Act
        var action = () => message.UpdateContent("Updated");

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Only user messages can be edited*");
    }

    [Fact]
    public void UpdateContent_OnDeletedMessage_ThrowsInvalidOperationException()
    {
        // Arrange
        var message = new ChatMessage("Content", ChatMessage.UserRole, 0);
        message.Delete(Guid.NewGuid());

        // Act
        var action = () => message.UpdateContent("Updated");

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot edit deleted message*");
    }

    [Fact]
    public void UpdateContent_WithEmptyContent_ThrowsValidationException()
    {
        // Arrange
        var message = new ChatMessage("Content", ChatMessage.UserRole, 0);

        // Act
        var action = () => message.UpdateContent("");

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Message content cannot be empty*");
    }

    [Fact]
    public void UpdateContent_WithTooLongContent_ThrowsValidationException()
    {
        // Arrange
        var message = new ChatMessage("Content", ChatMessage.UserRole, 0);
        var longContent = new string('a', 10001);

        // Act
        var action = () => message.UpdateContent(longContent);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Message content cannot exceed 10,000 characters*");
    }

    [Fact]
    public void UpdateContent_TrimsWhitespace()
    {
        // Arrange
        var message = new ChatMessage("Content", ChatMessage.UserRole, 0);

        // Act
        message.UpdateContent("  Trimmed  ");

        // Assert
        message.Content.Should().Be("Trimmed");
    }

    #endregion

    #region Delete Tests

    [Fact]
    public void Delete_MarksMessageAsDeleted()
    {
        // Arrange
        var message = new ChatMessage("Content", ChatMessage.UserRole, 0);
        var deletedByUserId = Guid.NewGuid();

        // Act
        message.Delete(deletedByUserId);

        // Assert
        message.IsDeleted.Should().BeTrue();
        message.DeletedAt.Should().NotBeNull();
        message.DeletedByUserId.Should().Be(deletedByUserId);
    }

    [Fact]
    public void Delete_WhenAlreadyDeleted_IsIdempotent()
    {
        // Arrange
        var message = new ChatMessage("Content", ChatMessage.UserRole, 0);
        var firstDeletedBy = Guid.NewGuid();
        var secondDeletedBy = Guid.NewGuid();
        message.Delete(firstDeletedBy);
        var firstDeletedAt = message.DeletedAt;

        // Act
        message.Delete(secondDeletedBy);

        // Assert
        message.DeletedByUserId.Should().Be(firstDeletedBy); // Should keep first deleter
        message.DeletedAt.Should().Be(firstDeletedAt); // Should keep first time
    }

    #endregion

    #region Invalidate Tests

    [Fact]
    public void Invalidate_OnAssistantMessage_MarksAsInvalidated()
    {
        // Arrange
        var message = new ChatMessage("Content", ChatMessage.AssistantRole, 0);

        // Act
        message.Invalidate();

        // Assert
        message.IsInvalidated.Should().BeTrue();
    }

    [Fact]
    public void Invalidate_OnUserMessage_ThrowsInvalidOperationException()
    {
        // Arrange
        var message = new ChatMessage("Content", ChatMessage.UserRole, 0);

        // Act
        var action = () => message.Invalidate();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Only assistant messages can be invalidated*");
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var message = new ChatMessage("Hello, world!", ChatMessage.UserRole, 0);

        // Act
        var result = message.ToString();

        // Assert
        result.Should().Contain("[user]");
        result.Should().Contain("Hello, world!");
    }

    [Fact]
    public void ToString_TruncatesLongContent()
    {
        // Arrange
        var longContent = new string('a', 100);
        var message = new ChatMessage(longContent, ChatMessage.UserRole, 0);

        // Act
        var result = message.ToString();

        // Assert
        result.Should().Contain("...");
        result.Length.Should().BeLessThan(longContent.Length + 20); // Some overhead for role prefix
    }

    #endregion

    #region Static Role Values Tests

    [Fact]
    public void UserRole_HasCorrectValue()
    {
        // Assert
        ChatMessage.UserRole.Should().Be("user");
    }

    [Fact]
    public void AssistantRole_HasCorrectValue()
    {
        // Assert
        ChatMessage.AssistantRole.Should().Be("assistant");
    }

    #endregion
}
