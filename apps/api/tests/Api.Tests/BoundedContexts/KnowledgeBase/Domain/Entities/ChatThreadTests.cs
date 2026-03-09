using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Tests for the ChatThread aggregate root.
/// Issue #3025: Backend 90% Coverage Target - Phase 17
/// </summary>
[Trait("Category", "Unit")]
public sealed class ChatThreadTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesChatThread()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var title = "Test Thread";
        var agentId = Guid.NewGuid();

        // Act
        var thread = new ChatThread(id, userId, gameId, title, agentId);

        // Assert
        thread.Id.Should().Be(id);
        thread.UserId.Should().Be(userId);
        thread.GameId.Should().Be(gameId);
        thread.Title.Should().Be(title);
        thread.AgentId.Should().Be(agentId);
        thread.Status.Should().Be(ThreadStatus.Active);
        thread.Messages.Should().BeEmpty();
        thread.MessageCount.Should().Be(0);
        thread.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithMinimalParameters_CreatesChatThread()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var thread = new ChatThread(id, userId);

        // Assert
        thread.Id.Should().Be(id);
        thread.UserId.Should().Be(userId);
        thread.GameId.Should().BeNull();
        thread.Title.Should().BeNull();
        thread.AgentId.Should().BeNull();
        thread.Status.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var emptyUserId = Guid.Empty;

        // Act
        var action = () => new ChatThread(id, emptyUserId);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("userId")
            .WithMessage("*UserId cannot be empty*");
    }

    [Fact]
    public void Constructor_WithTitleContainingWhitespace_TrimsTitle()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var title = "  Test Title  ";

        // Act
        var thread = new ChatThread(id, userId, title: title);

        // Assert
        thread.Title.Should().Be("Test Title");
    }

    [Fact]
    public void Constructor_RaisesChatThreadCreatedEvent()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var thread = new ChatThread(id, userId, gameId);

        // Assert
        thread.DomainEvents.Should().HaveCount(1);
        var domainEvent = thread.DomainEvents.First();
        domainEvent.Should().BeAssignableTo<object>();
    }

    #endregion

    #region AddMessage Tests

    [Fact]
    public void AddMessage_WithValidMessage_AddsToThread()
    {
        // Arrange
        var thread = CreateActiveThread();
        var message = new ChatMessage("Test content", ChatMessage.UserRole, 0);

        // Act
        thread.AddMessage(message);

        // Assert
        thread.Messages.Should().HaveCount(1);
        thread.MessageCount.Should().Be(1);
        thread.IsEmpty.Should().BeFalse();
        thread.LastMessage.Should().Be(message);
    }

    [Fact]
    public void AddMessage_WithNullMessage_ThrowsArgumentNullException()
    {
        // Arrange
        var thread = CreateActiveThread();

        // Act
        var action = () => thread.AddMessage(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddMessage_ToClosedThread_ThrowsInvalidOperationException()
    {
        // Arrange
        var thread = CreateActiveThread();
        thread.CloseThread();
        var message = new ChatMessage("Test content", ChatMessage.UserRole, 0);

        // Act
        var action = () => thread.AddMessage(message);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot add message to closed thread*");
    }

    [Fact]
    public void AddMessage_UpdatesLastMessageAt()
    {
        // Arrange
        var thread = CreateActiveThread();
        var initialLastMessageAt = thread.LastMessageAt;
        var message = new ChatMessage("Test content", ChatMessage.UserRole, 0);

        // Act
        thread.AddMessage(message);

        // Assert
        thread.LastMessageAt.Should().Be(message.Timestamp);
    }

    [Fact]
    public void AddMessage_RaisesMessageAddedEvent()
    {
        // Arrange
        var thread = CreateActiveThread();
        thread.ClearDomainEvents();
        var message = new ChatMessage("Test content", ChatMessage.UserRole, 0);

        // Act
        thread.AddMessage(message);

        // Assert
        thread.DomainEvents.Should().HaveCount(1);
    }

    #endregion

    #region AddUserMessage Tests

    [Fact]
    public void AddUserMessage_WithValidContent_AddsUserMessage()
    {
        // Arrange
        var thread = CreateActiveThread();

        // Act
        thread.AddUserMessage("Hello, world!");

        // Assert
        thread.Messages.Should().HaveCount(1);
        thread.LastMessage!.Role.Should().Be(ChatMessage.UserRole);
        thread.LastMessage!.Content.Should().Be("Hello, world!");
        thread.LastMessage!.SequenceNumber.Should().Be(0);
    }

    [Fact]
    public void AddUserMessage_MultipleTimes_AssignsCorrectSequenceNumbers()
    {
        // Arrange
        var thread = CreateActiveThread();

        // Act
        thread.AddUserMessage("First message");
        thread.AddUserMessage("Second message");
        thread.AddUserMessage("Third message");

        // Assert
        thread.Messages.Should().HaveCount(3);
        thread.Messages[0].SequenceNumber.Should().Be(0);
        thread.Messages[1].SequenceNumber.Should().Be(1);
        thread.Messages[2].SequenceNumber.Should().Be(2);
    }

    #endregion

    #region AddAssistantMessage Tests

    [Fact]
    public void AddAssistantMessage_WithValidContent_AddsAssistantMessage()
    {
        // Arrange
        var thread = CreateActiveThread();

        // Act
        thread.AddAssistantMessage("I can help with that!");

        // Assert
        thread.Messages.Should().HaveCount(1);
        thread.LastMessage!.Role.Should().Be(ChatMessage.AssistantRole);
        thread.LastMessage!.Content.Should().Be("I can help with that!");
    }

    [Fact]
    public void AddAssistantMessage_AssignsCorrectSequenceNumber()
    {
        // Arrange
        var thread = CreateActiveThread();
        thread.AddUserMessage("User question");

        // Act
        thread.AddAssistantMessage("Assistant answer");

        // Assert
        thread.Messages[1].SequenceNumber.Should().Be(1);
    }

    #endregion

    #region UpdateMessage Tests

    [Fact]
    public void UpdateMessage_WithValidMessageId_UpdatesContent()
    {
        // Arrange
        var thread = CreateActiveThread();
        thread.AddUserMessage("Original content");
        var messageId = thread.Messages[0].Id;
        var userId = Guid.NewGuid();

        // Act
        thread.UpdateMessage(messageId, "Updated content", userId);

        // Assert
        thread.Messages[0].Content.Should().Be("Updated content");
    }

    [Fact]
    public void UpdateMessage_WithNonExistentMessageId_ThrowsInvalidOperationException()
    {
        // Arrange
        var thread = CreateActiveThread();
        var nonExistentId = Guid.NewGuid();

        // Act
        var action = () => thread.UpdateMessage(nonExistentId, "New content", Guid.NewGuid());

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"*Message {nonExistentId} not found in thread*");
    }

    [Fact]
    public void UpdateMessage_InvalidatesSubsequentAssistantMessages()
    {
        // Arrange
        var thread = CreateActiveThread();
        thread.AddUserMessage("User question");
        thread.AddAssistantMessage("Assistant answer");
        thread.AddUserMessage("Follow-up question");
        thread.AddAssistantMessage("Follow-up answer");
        var firstMessageId = thread.Messages[0].Id;

        // Act
        thread.UpdateMessage(firstMessageId, "Updated question", Guid.NewGuid());

        // Assert
        thread.Messages[1].IsInvalidated.Should().BeTrue(); // First assistant message
        thread.Messages[3].IsInvalidated.Should().BeTrue(); // Second assistant message
        thread.Messages[2].IsInvalidated.Should().BeFalse(); // User message not invalidated
    }

    [Fact]
    public void UpdateMessage_RaisesMessageUpdatedEvent()
    {
        // Arrange
        var thread = CreateActiveThread();
        thread.AddUserMessage("Original content");
        thread.ClearDomainEvents();
        var messageId = thread.Messages[0].Id;

        // Act
        thread.UpdateMessage(messageId, "Updated content", Guid.NewGuid());

        // Assert
        thread.DomainEvents.Should().HaveCount(1);
    }

    #endregion

    #region DeleteMessage Tests

    [Fact]
    public void DeleteMessage_WithValidMessageId_SoftDeletesMessage()
    {
        // Arrange
        var thread = CreateActiveThread();
        thread.AddUserMessage("Test content");
        var messageId = thread.Messages[0].Id;
        var deletedByUserId = Guid.NewGuid();

        // Act
        thread.DeleteMessage(messageId, deletedByUserId);

        // Assert
        thread.Messages[0].IsDeleted.Should().BeTrue();
        thread.Messages[0].DeletedByUserId.Should().Be(deletedByUserId);
    }

    [Fact]
    public void DeleteMessage_WithNonExistentMessageId_ThrowsInvalidOperationException()
    {
        // Arrange
        var thread = CreateActiveThread();
        var nonExistentId = Guid.NewGuid();

        // Act
        var action = () => thread.DeleteMessage(nonExistentId, Guid.NewGuid());

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"*Message {nonExistentId} not found in thread*");
    }

    [Fact]
    public void DeleteMessage_InvalidatesSubsequentAssistantMessages()
    {
        // Arrange
        var thread = CreateActiveThread();
        thread.AddUserMessage("User question");
        thread.AddAssistantMessage("Assistant answer");
        var firstMessageId = thread.Messages[0].Id;

        // Act
        thread.DeleteMessage(firstMessageId, Guid.NewGuid());

        // Assert
        thread.Messages[1].IsInvalidated.Should().BeTrue();
    }

    [Fact]
    public void DeleteMessage_AsAdmin_DeletesAnyMessage()
    {
        // Arrange
        var thread = CreateActiveThread();
        thread.AddAssistantMessage("Assistant message");
        var messageId = thread.Messages[0].Id;

        // Act
        thread.DeleteMessage(messageId, Guid.NewGuid(), isAdmin: true);

        // Assert
        thread.Messages[0].IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void DeleteMessage_RaisesMessageDeletedEvent()
    {
        // Arrange
        var thread = CreateActiveThread();
        thread.AddUserMessage("Test content");
        thread.ClearDomainEvents();
        var messageId = thread.Messages[0].Id;

        // Act
        thread.DeleteMessage(messageId, Guid.NewGuid());

        // Assert
        thread.DomainEvents.Should().HaveCount(1);
    }

    #endregion

    #region GetMessageById Tests

    [Fact]
    public void GetMessageById_WithExistingId_ReturnsMessage()
    {
        // Arrange
        var thread = CreateActiveThread();
        thread.AddUserMessage("Test content");
        var messageId = thread.Messages[0].Id;

        // Act
        var result = thread.GetMessageById(messageId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(messageId);
    }

    [Fact]
    public void GetMessageById_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var thread = CreateActiveThread();

        // Act
        var result = thread.GetMessageById(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region SetTitle Tests

    [Fact]
    public void SetTitle_WithValidTitle_SetsTitle()
    {
        // Arrange
        var thread = CreateActiveThread();

        // Act
        thread.SetTitle("New Title");

        // Assert
        thread.Title.Should().Be("New Title");
    }

    [Fact]
    public void SetTitle_WithWhitespace_TrimsTitle()
    {
        // Arrange
        var thread = CreateActiveThread();

        // Act
        thread.SetTitle("  Trimmed Title  ");

        // Assert
        thread.Title.Should().Be("Trimmed Title");
    }

    [Fact]
    public void SetTitle_WithEmptyString_ThrowsArgumentException()
    {
        // Arrange
        var thread = CreateActiveThread();

        // Act
        var action = () => thread.SetTitle("");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Title cannot be empty*");
    }

    [Fact]
    public void SetTitle_WithWhitespaceOnly_ThrowsArgumentException()
    {
        // Arrange
        var thread = CreateActiveThread();

        // Act
        var action = () => thread.SetTitle("   ");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Title cannot be empty*");
    }

    [Fact]
    public void SetTitle_WithTooLongTitle_ThrowsArgumentException()
    {
        // Arrange
        var thread = CreateActiveThread();
        var longTitle = new string('a', 201);

        // Act
        var action = () => thread.SetTitle(longTitle);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Title cannot exceed 200 characters*");
    }

    [Fact]
    public void SetTitle_WithExactly200Characters_Succeeds()
    {
        // Arrange
        var thread = CreateActiveThread();
        var title = new string('a', 200);

        // Act
        thread.SetTitle(title);

        // Assert
        thread.Title.Should().HaveLength(200);
    }

    #endregion

    #region CloseThread Tests

    [Fact]
    public void CloseThread_WhenActive_ClosesThread()
    {
        // Arrange
        var thread = CreateActiveThread();

        // Act
        thread.CloseThread();

        // Assert
        thread.Status.Should().Be(ThreadStatus.Closed);
        thread.Status.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void CloseThread_WhenAlreadyClosed_ThrowsInvalidOperationException()
    {
        // Arrange
        var thread = CreateActiveThread();
        thread.CloseThread();

        // Act
        var action = () => thread.CloseThread();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Thread is already closed*");
    }

    [Fact]
    public void CloseThread_RaisesThreadClosedEvent()
    {
        // Arrange
        var thread = CreateActiveThread();
        thread.ClearDomainEvents();

        // Act
        thread.CloseThread();

        // Assert
        thread.DomainEvents.Should().HaveCount(1);
    }

    #endregion

    #region ReopenThread Tests

    [Fact]
    public void ReopenThread_WhenClosed_ReopensThread()
    {
        // Arrange
        var thread = CreateActiveThread();
        thread.CloseThread();

        // Act
        thread.ReopenThread();

        // Assert
        thread.Status.Should().Be(ThreadStatus.Active);
        thread.Status.IsActive.Should().BeTrue();
    }

    [Fact]
    public void ReopenThread_WhenAlreadyActive_ThrowsInvalidOperationException()
    {
        // Arrange
        var thread = CreateActiveThread();

        // Act
        var action = () => thread.ReopenThread();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Thread is already active*");
    }

    [Fact]
    public void ReopenThread_RaisesThreadReopenedEvent()
    {
        // Arrange
        var thread = CreateActiveThread();
        thread.CloseThread();
        thread.ClearDomainEvents();

        // Act
        thread.ReopenThread();

        // Assert
        thread.DomainEvents.Should().HaveCount(1);
    }

    #endregion

    #region Export Tests - JSON

    [Fact]
    public void Export_AsJson_ReturnsValidJsonExport()
    {
        // Arrange
        var thread = CreateActiveThread();
        thread.SetTitle("Test Chat");
        thread.AddUserMessage("Hello");
        thread.AddAssistantMessage("Hi there!");

        // Act
        var result = thread.Export(ExportFormat.Json);

        // Assert
        result.Should().NotBeNull();
        result.Format.Should().Be(ExportFormat.Json);
        result.ContentType.Should().Be("application/json");
        result.FileExtension.Should().Be("json");
        result.Content.Should().Contain("Test Chat");
        result.Content.Should().Contain("Hello");
        result.Content.Should().Contain("Hi there!");
    }

    [Fact]
    public void Export_AsJson_ContainsAllMessageFields()
    {
        // Arrange
        var thread = CreateActiveThread();
        thread.AddUserMessage("Test message");

        // Act
        var result = thread.Export(ExportFormat.Json);

        // Assert
        result.Content.Should().Contain("\"role\"");
        result.Content.Should().Contain("\"content\"");
        result.Content.Should().Contain("\"timestamp\"");
    }

    [Fact]
    public void Export_AsJson_WithNoTitle_UsesUntitledChat()
    {
        // Arrange
        var thread = CreateActiveThread();

        // Act
        var result = thread.Export(ExportFormat.Json);

        // Assert
        result.Content.Should().Contain("Untitled Chat");
    }

    #endregion

    #region Export Tests - Markdown

    [Fact]
    public void Export_AsMarkdown_ReturnsValidMarkdownExport()
    {
        // Arrange
        var thread = CreateActiveThread();
        thread.SetTitle("Test Chat");
        thread.AddUserMessage("Hello");
        thread.AddAssistantMessage("Hi there!");

        // Act
        var result = thread.Export(ExportFormat.Markdown);

        // Assert
        result.Should().NotBeNull();
        result.Format.Should().Be(ExportFormat.Markdown);
        result.ContentType.Should().Be("text/markdown");
        result.FileExtension.Should().Be("md");
        result.Content.Should().Contain("# Test Chat");
        result.Content.Should().Contain("Hello");
        result.Content.Should().Contain("Hi there!");
    }

    [Fact]
    public void Export_AsMarkdown_ContainsHeaderInformation()
    {
        // Arrange
        var thread = CreateActiveThread();
        thread.SetTitle("Test Chat");

        // Act
        var result = thread.Export(ExportFormat.Markdown);

        // Assert
        result.Content.Should().Contain("**Created:**");
        result.Content.Should().Contain("**Last Activity:**");
        result.Content.Should().Contain("**Status:**");
        result.Content.Should().Contain("**Messages:**");
    }

    [Fact]
    public void Export_AsMarkdown_ContainsRoleEmojis()
    {
        // Arrange
        var thread = CreateActiveThread();
        thread.AddUserMessage("User message");
        thread.AddAssistantMessage("Assistant message");

        // Act
        var result = thread.Export(ExportFormat.Markdown);

        // Assert
        result.Content.Should().Contain("👤 User");
        result.Content.Should().Contain("🤖 Assistant");
    }

    [Fact]
    public void Export_AsMarkdown_WithNoMessages_ShowsNoMessagesText()
    {
        // Arrange
        var thread = CreateActiveThread();

        // Act
        var result = thread.Export(ExportFormat.Markdown);

        // Assert
        result.Content.Should().Contain("*No messages in this conversation.*");
    }

    [Fact]
    public void Export_AsMarkdown_ContainsExportFooter()
    {
        // Arrange
        var thread = CreateActiveThread();

        // Act
        var result = thread.Export(ExportFormat.Markdown);

        // Assert
        result.Content.Should().Contain("*Exported on");
    }

    #endregion

    #region Export Tests - Invalid Format

    [Fact]
    public void Export_WithInvalidFormat_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var thread = CreateActiveThread();
        var invalidFormat = (ExportFormat)999;

        // Act
        var action = () => thread.Export(invalidFormat);

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Helper Methods

    private static ChatThread CreateActiveThread()
    {
        return new ChatThread(Guid.NewGuid(), Guid.NewGuid());
    }

    #endregion

    #region ConversationSummary Tests (Issue #5259)

    [Fact]
    public void UpdateConversationSummary_WithValidSummary_SetsSummary()
    {
        // Arrange
        var thread = CreateActiveThread();

        // Act
        thread.UpdateConversationSummary("User discussed Catan setup rules.");

        // Assert
        thread.ConversationSummary.Should().Be("User discussed Catan setup rules.");
    }

    [Fact]
    public void UpdateConversationSummary_TrimsWhitespace()
    {
        var thread = CreateActiveThread();
        thread.UpdateConversationSummary("  Summary with spaces  ");

        thread.ConversationSummary.Should().Be("Summary with spaces");
    }

    [Fact]
    public void UpdateConversationSummary_WithEmpty_Throws()
    {
        var thread = CreateActiveThread();
        var act = () => thread.UpdateConversationSummary("");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateConversationSummary_WithWhitespace_Throws()
    {
        var thread = CreateActiveThread();
        var act = () => thread.UpdateConversationSummary("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateConversationSummary_CanBeUpdatedMultipleTimes()
    {
        var thread = CreateActiveThread();

        thread.UpdateConversationSummary("First summary.");
        thread.ConversationSummary.Should().Be("First summary.");

        thread.UpdateConversationSummary("Updated summary with more context.");
        thread.ConversationSummary.Should().Be("Updated summary with more context.");
    }

    [Fact]
    public void ConversationSummary_DefaultsToNull()
    {
        var thread = CreateActiveThread();
        thread.ConversationSummary.Should().BeNull();
    }

    [Fact]
    public void UpdateConversationSummary_SetsLastSummarizedMessageCount()
    {
        var thread = CreateActiveThread();
        thread.AddUserMessage("Q1");
        thread.AddAssistantMessage("A1");
        thread.AddUserMessage("Q2");
        thread.AddAssistantMessage("A2");

        thread.UpdateConversationSummary("Summary of 4 messages.");

        thread.LastSummarizedMessageCount.Should().Be(4);
    }

    [Fact]
    public void UpdateConversationSummary_UpdatesLastSummarizedMessageCount_OnSubsequentCalls()
    {
        var thread = CreateActiveThread();
        for (int i = 0; i < 6; i++)
            thread.AddUserMessage($"Q{i}");

        thread.UpdateConversationSummary("First summary.");
        thread.LastSummarizedMessageCount.Should().Be(6);

        thread.AddUserMessage("Q6");
        thread.AddUserMessage("Q7");
        thread.UpdateConversationSummary("Updated summary.");
        thread.LastSummarizedMessageCount.Should().Be(8);
    }

    [Fact]
    public void LastSummarizedMessageCount_DefaultsToZero()
    {
        var thread = CreateActiveThread();
        thread.LastSummarizedMessageCount.Should().Be(0);
    }

    #endregion
}
