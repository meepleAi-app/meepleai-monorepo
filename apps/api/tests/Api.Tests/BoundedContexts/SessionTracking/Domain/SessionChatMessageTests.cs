using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

/// <summary>
/// Unit tests for SessionChatMessage domain entity.
/// Issue #4760
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class SessionChatMessageTests
{
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _senderId = Guid.NewGuid();

    [Fact]
    public void CreateTextMessage_ValidInput_CreatesMessage()
    {
        // Act
        var msg = SessionChatMessage.CreateTextMessage(
            _sessionId, _senderId, "Hello everyone!", sequenceNumber: 1, turnNumber: 3, mentionsJson: "[\"@alice\"]");

        // Assert
        msg.Id.Should().NotBe(Guid.Empty);
        msg.SessionId.Should().Be(_sessionId);
        msg.SenderId.Should().Be(_senderId);
        msg.Content.Should().Be("Hello everyone!");
        msg.MessageType.Should().Be(SessionChatMessageType.Text);
        msg.SequenceNumber.Should().Be(1);
        msg.TurnNumber.Should().Be(3);
        msg.MentionsJson.Should().Be("[\"@alice\"]");
        msg.AgentType.Should().BeNull();
        msg.Confidence.Should().BeNull();
        msg.CitationsJson.Should().BeNull();
        msg.IsDeleted.Should().BeFalse();
        msg.DeletedAt.Should().BeNull();
        msg.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void CreateTextMessage_TrimsContent()
    {
        var msg = SessionChatMessage.CreateTextMessage(
            _sessionId, _senderId, "  spaced  ", sequenceNumber: 1);
        msg.Content.Should().Be("spaced");
    }

    [Fact]
    public void CreateTextMessage_EmptySessionId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            SessionChatMessage.CreateTextMessage(Guid.Empty, _senderId, "content", 1));
    }

    [Fact]
    public void CreateTextMessage_EmptySenderId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            SessionChatMessage.CreateTextMessage(_sessionId, Guid.Empty, "content", 1));
    }

    [Fact]
    public void CreateTextMessage_EmptyContent_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            SessionChatMessage.CreateTextMessage(_sessionId, _senderId, "", 1));
        Assert.Throws<ArgumentException>(() =>
            SessionChatMessage.CreateTextMessage(_sessionId, _senderId, "   ", 1));
    }

    [Fact]
    public void CreateTextMessage_ContentExceeds5000Chars_ThrowsArgumentException()
    {
        var longContent = new string('A', 5001);
        Assert.Throws<ArgumentException>(() =>
            SessionChatMessage.CreateTextMessage(_sessionId, _senderId, longContent, 1));
    }

    [Fact]
    public void CreateTextMessage_ContentExactly5000Chars_Succeeds()
    {
        var content = new string('A', 5000);
        var msg = SessionChatMessage.CreateTextMessage(_sessionId, _senderId, content, 1);
        msg.Content.Length.Should().Be(5000);
    }

    [Fact]
    public void CreateSystemEvent_ValidInput_CreatesSystemMessage()
    {
        // Act
        var msg = SessionChatMessage.CreateSystemEvent(
            _sessionId, "Alice joined the session", sequenceNumber: 2, turnNumber: 1);

        // Assert
        msg.SessionId.Should().Be(_sessionId);
        msg.SenderId.Should().BeNull();
        msg.Content.Should().Be("Alice joined the session");
        msg.MessageType.Should().Be(SessionChatMessageType.SystemEvent);
        msg.SequenceNumber.Should().Be(2);
        msg.TurnNumber.Should().Be(1);
    }

    [Fact]
    public void CreateSystemEvent_EmptySessionId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            SessionChatMessage.CreateSystemEvent(Guid.Empty, "content", 1));
    }

    [Fact]
    public void CreateSystemEvent_EmptyContent_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            SessionChatMessage.CreateSystemEvent(_sessionId, "", 1));
    }

    [Fact]
    public void CreateAgentResponse_ValidInput_CreatesAgentMessage()
    {
        // Act
        var msg = SessionChatMessage.CreateAgentResponse(
            _sessionId,
            "The rules state that...",
            sequenceNumber: 5,
            agentType: "tutor",
            confidence: 0.92f,
            citationsJson: "[{\"source\":\"rulebook\"}]",
            turnNumber: 3);

        // Assert
        msg.SessionId.Should().Be(_sessionId);
        msg.SenderId.Should().BeNull();
        msg.Content.Should().Be("The rules state that...");
        msg.MessageType.Should().Be(SessionChatMessageType.AgentResponse);
        msg.SequenceNumber.Should().Be(5);
        msg.AgentType.Should().Be("tutor");
        msg.Confidence.Should().Be(0.92f);
        msg.CitationsJson.Should().Be("[{\"source\":\"rulebook\"}]");
        msg.TurnNumber.Should().Be(3);
    }

    [Fact]
    public void CreateAgentResponse_EmptySessionId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            SessionChatMessage.CreateAgentResponse(Guid.Empty, "content", 1, "tutor"));
    }

    [Fact]
    public void CreateAgentResponse_EmptyContent_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            SessionChatMessage.CreateAgentResponse(_sessionId, "", 1, "tutor"));
    }

    [Fact]
    public void CreateAgentResponse_EmptyAgentType_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            SessionChatMessage.CreateAgentResponse(_sessionId, "content", 1, ""));
        Assert.Throws<ArgumentException>(() =>
            SessionChatMessage.CreateAgentResponse(_sessionId, "content", 1, "   "));
    }

    [Fact]
    public void UpdateContent_TextMessage_UpdatesContent()
    {
        // Arrange
        var msg = SessionChatMessage.CreateTextMessage(_sessionId, _senderId, "original", 1);

        // Act
        Thread.Sleep(10);
        msg.UpdateContent("updated content");

        // Assert
        msg.Content.Should().Be("updated content");
        msg.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateContent_TrimsWhitespace()
    {
        var msg = SessionChatMessage.CreateTextMessage(_sessionId, _senderId, "original", 1);
        msg.UpdateContent("  trimmed  ");
        msg.Content.Should().Be("trimmed");
    }

    [Fact]
    public void UpdateContent_SystemEvent_ThrowsInvalidOperationException()
    {
        var msg = SessionChatMessage.CreateSystemEvent(_sessionId, "event", 1);
        ((Action)(() => msg.UpdateContent("new content"))).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateContent_AgentResponse_ThrowsInvalidOperationException()
    {
        var msg = SessionChatMessage.CreateAgentResponse(_sessionId, "answer", 1, "tutor");
        ((Action)(() => msg.UpdateContent("new content"))).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateContent_EmptyContent_ThrowsArgumentException()
    {
        var msg = SessionChatMessage.CreateTextMessage(_sessionId, _senderId, "original", 1);
        ((Action)(() => msg.UpdateContent(""))).Should().Throw<ArgumentException>();
        ((Action)(() => msg.UpdateContent("   "))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateContent_ContentExceeds5000Chars_ThrowsArgumentException()
    {
        var msg = SessionChatMessage.CreateTextMessage(_sessionId, _senderId, "original", 1);
        var longContent = new string('A', 5001);
        ((Action)(() => msg.UpdateContent(longContent))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SoftDelete_SetsDeletedFlags()
    {
        // Arrange
        var msg = SessionChatMessage.CreateTextMessage(_sessionId, _senderId, "content", 1);

        // Act
        msg.SoftDelete();

        // Assert
        msg.IsDeleted.Should().BeTrue();
        msg.DeletedAt.Should().NotBeNull();
        msg.UpdatedAt.Should().NotBeNull();
    }
}
