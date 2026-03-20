using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Tests for ChatSession aggregate root.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ChatSessionTests
{
    [Fact]
    public void ChatSession_Create_InitializesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var title = "Test Session";

        // Act
        var session = new ChatSession(id, userId, gameId, title);

        // Assert
        session.Id.Should().Be(id);
        session.UserId.Should().Be(userId);
        session.GameId.Should().Be(gameId);
        session.Title.Should().Be(title);
        session.AgentConfigJson.Should().Be("{}");
        Assert.False(session.IsArchived);
        session.MessageCount.Should().Be(0);
        Assert.False(session.HasMessages);
        Assert.Null(session.LastMessage);
        Assert.Null(session.UserLibraryEntryId);
        Assert.Null(session.AgentSessionId);
    }

    [Fact]
    public void ChatSession_Create_WithAllParameters_InitializesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userLibraryEntryId = Guid.NewGuid();
        var agentSessionId = Guid.NewGuid();
        var agentConfigJson = "{\"model\":\"claude-3\"}";

        // Act
        var session = new ChatSession(
            id,
            userId,
            gameId,
            "Full Session",
            userLibraryEntryId,
            agentSessionId,
            agentConfigJson);

        // Assert
        session.UserLibraryEntryId.Should().Be(userLibraryEntryId);
        session.AgentSessionId.Should().Be(agentSessionId);
        session.AgentConfigJson.Should().Be(agentConfigJson);
    }

    [Fact]
    public void ChatSession_Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new ChatSession(Guid.NewGuid(), Guid.Empty, Guid.NewGuid()));
        ex.Message.Should().Contain("UserId cannot be empty");
    }

    [Fact]
    public void ChatSession_Create_WithEmptyGameId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty));
        ex.Message.Should().Contain("GameId cannot be empty");
    }

    [Fact]
    public void ChatSession_Create_TrimsTitle()
    {
        // Arrange & Act
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "  Padded Title  ");

        // Assert
        session.Title.Should().Be("Padded Title");
    }

    [Fact]
    public void ChatSession_Create_SetsCreatedAtAndLastMessageAt()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Assert
        Assert.InRange(session.CreatedAt, before, DateTime.UtcNow.AddSeconds(1));
        session.LastMessageAt.Should().Be(session.CreatedAt);
    }

    [Fact]
    public void ChatSession_Create_RaisesChatSessionCreatedEvent()
    {
        // Arrange & Act
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Assert
        var events = session.DomainEvents.ToList();
        events.Should().ContainSingle();
        Assert.IsType<Api.BoundedContexts.KnowledgeBase.Domain.Events.ChatSessionCreatedEvent>(events[0]);
    }

    [Fact]
    public void AddUserMessage_AddsMessageCorrectly()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        session.AddUserMessage("Hello, I need help with this game");

        // Assert
        session.MessageCount.Should().Be(1);
        Assert.True(session.HasMessages);
        Assert.NotNull(session.LastMessage);
        Assert.Equal("Hello, I need help with this game", session.LastMessage!.Content);
        Assert.True(session.LastMessage.IsUserMessage);
        session.LastMessage.SequenceNumber.Should().Be(0);
    }

    [Fact]
    public void AddAssistantMessage_AddsMessageCorrectly()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        session.AddAssistantMessage("Sure, I can help you with that!");

        // Assert
        session.MessageCount.Should().Be(1);
        Assert.NotNull(session.LastMessage);
        Assert.True(session.LastMessage!.IsAssistantMessage);
    }

    [Fact]
    public void AddSystemMessage_AddsMessageCorrectly()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        session.AddSystemMessage("You are a helpful board game assistant.");

        // Assert
        session.MessageCount.Should().Be(1);
        Assert.NotNull(session.LastMessage);
        Assert.True(session.LastMessage!.IsSystemMessage);
    }

    [Fact]
    public void AddMessage_WithMetadata_IncludesMetadata()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var metadata = new Dictionary<string, object>
        {
            { "tokenCount", 150 },
            { "model", "claude-3" }
        };

        // Act
        session.AddUserMessage("Test message", metadata);

        // Assert
        Assert.NotNull(session.LastMessage!.MetadataJson);
    }

    [Fact]
    public void AddMessage_UpdatesLastMessageAt()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var initialTimestamp = session.LastMessageAt;

        // Small delay to ensure timestamp difference
        System.Threading.Thread.Sleep(10);

        // Act
        session.AddUserMessage("Test message");

        // Assert
        Assert.True(session.LastMessageAt >= initialTimestamp);
    }

    [Fact]
    public void AddMessage_RaisesMessageAddedEvent()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        session.ClearDomainEvents(); // Clear the created event

        // Act
        session.AddUserMessage("Test message");

        // Assert
        var events = session.DomainEvents.ToList();
        events.Should().ContainSingle();
        Assert.IsType<Api.BoundedContexts.KnowledgeBase.Domain.Events.ChatSessionMessageAddedEvent>(events[0]);
    }

    [Fact]
    public void AddMessage_ToArchivedSession_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        session.Archive();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            session.AddUserMessage("Test"));
        ex.Message.Should().Contain("archived session");
    }

    [Fact]
    public void AddMessage_WithNullMessage_ThrowsArgumentNullException()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            session.AddMessage(null!));
    }

    [Fact]
    public void MultipleMessages_AssignsCorrectSequenceNumbers()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        session.AddUserMessage("Message 1");
        session.AddAssistantMessage("Message 2");
        session.AddUserMessage("Message 3");

        // Assert
        var messages = session.Messages.ToList();
        messages.Count.Should().Be(3);
        messages[0].SequenceNumber.Should().Be(0);
        messages[1].SequenceNumber.Should().Be(1);
        messages[2].SequenceNumber.Should().Be(2);
    }

    [Fact]
    public void SetTitle_UpdatesTitle()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        session.SetTitle("New Title");

        // Assert
        session.Title.Should().Be("New Title");
    }

    [Fact]
    public void SetTitle_TrimsWhitespace()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        session.SetTitle("  Trimmed  ");

        // Assert
        session.Title.Should().Be("Trimmed");
    }

    [Fact]
    public void SetTitle_WithEmptyString_ThrowsArgumentException()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        ((Action)(() => session.SetTitle(""))).Should().Throw<ArgumentException>();
        ((Action)(() => session.SetTitle("   "))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetTitle_ExceedingMaxLength_ThrowsArgumentException()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var longTitle = new string('a', 201);

        // Act & Assert
        var ex = ((Action)(() => session.SetTitle(longTitle))).Should().Throw<ArgumentException>().Which;
        ex.Message.Should().Contain("cannot exceed 200 characters");
    }

    [Fact]
    public void UpdateAgentConfig_UpdatesConfig()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var newConfig = "{\"model\":\"gpt-4\",\"temperature\":0.7}";

        // Act
        session.UpdateAgentConfig(newConfig);

        // Assert
        session.AgentConfigJson.Should().Be(newConfig);
    }

    [Fact]
    public void UpdateAgentConfig_WithEmptyString_ThrowsArgumentException()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        ((Action)(() => session.UpdateAgentConfig(""))).Should().Throw<ArgumentException>();
        ((Action)(() => session.UpdateAgentConfig("   "))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LinkToAgentSession_LinksCorrectly()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var agentSessionId = Guid.NewGuid();

        // Act
        session.LinkToAgentSession(agentSessionId);

        // Assert
        session.AgentSessionId.Should().Be(agentSessionId);
    }

    [Fact]
    public void LinkToAgentSession_WithEmptyGuid_ThrowsArgumentException()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            session.LinkToAgentSession(Guid.Empty));
        ex.Message.Should().Contain("AgentSessionId cannot be empty");
    }

    [Fact]
    public void Archive_SetsIsArchivedToTrue()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        session.Archive();

        // Assert
        Assert.True(session.IsArchived);
    }

    [Fact]
    public void Archive_RaisesArchivedEvent()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        session.ClearDomainEvents();

        // Act
        session.Archive();

        // Assert
        var events = session.DomainEvents.ToList();
        events.Should().ContainSingle();
        Assert.IsType<Api.BoundedContexts.KnowledgeBase.Domain.Events.ChatSessionArchivedEvent>(events[0]);
    }

    [Fact]
    public void Archive_WhenAlreadyArchived_IsIdempotent()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        session.Archive();
        session.ClearDomainEvents();

        // Act
        session.Archive();

        // Assert
        Assert.True(session.IsArchived);
        Assert.Empty(session.DomainEvents); // No new events raised
    }

    [Fact]
    public void Unarchive_SetsIsArchivedToFalse()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        session.Archive();

        // Act
        session.Unarchive();

        // Assert
        Assert.False(session.IsArchived);
    }

    [Fact]
    public void Unarchive_WhenNotArchived_IsIdempotent()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        session.Unarchive();

        // Assert
        Assert.False(session.IsArchived);
    }

    [Fact]
    public void Unarchive_AllowsAddingMessagesAgain()
    {
        // Arrange
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        session.Archive();

        // Act
        session.Unarchive();
        session.AddUserMessage("New message after unarchive");

        // Assert
        session.MessageCount.Should().Be(1);
    }
}
