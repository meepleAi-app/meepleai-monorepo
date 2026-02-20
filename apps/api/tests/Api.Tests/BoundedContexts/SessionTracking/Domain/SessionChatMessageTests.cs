using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Tests.Constants;
using Xunit;

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
        Assert.NotEqual(Guid.Empty, msg.Id);
        Assert.Equal(_sessionId, msg.SessionId);
        Assert.Equal(_senderId, msg.SenderId);
        Assert.Equal("Hello everyone!", msg.Content);
        Assert.Equal(SessionChatMessageType.Text, msg.MessageType);
        Assert.Equal(1, msg.SequenceNumber);
        Assert.Equal(3, msg.TurnNumber);
        Assert.Equal("[\"@alice\"]", msg.MentionsJson);
        Assert.Null(msg.AgentType);
        Assert.Null(msg.Confidence);
        Assert.Null(msg.CitationsJson);
        Assert.False(msg.IsDeleted);
        Assert.Null(msg.DeletedAt);
        Assert.Null(msg.UpdatedAt);
    }

    [Fact]
    public void CreateTextMessage_TrimsContent()
    {
        var msg = SessionChatMessage.CreateTextMessage(
            _sessionId, _senderId, "  spaced  ", sequenceNumber: 1);
        Assert.Equal("spaced", msg.Content);
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
        Assert.Equal(5000, msg.Content.Length);
    }

    [Fact]
    public void CreateSystemEvent_ValidInput_CreatesSystemMessage()
    {
        // Act
        var msg = SessionChatMessage.CreateSystemEvent(
            _sessionId, "Alice joined the session", sequenceNumber: 2, turnNumber: 1);

        // Assert
        Assert.Equal(_sessionId, msg.SessionId);
        Assert.Null(msg.SenderId);
        Assert.Equal("Alice joined the session", msg.Content);
        Assert.Equal(SessionChatMessageType.SystemEvent, msg.MessageType);
        Assert.Equal(2, msg.SequenceNumber);
        Assert.Equal(1, msg.TurnNumber);
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
        Assert.Equal(_sessionId, msg.SessionId);
        Assert.Null(msg.SenderId);
        Assert.Equal("The rules state that...", msg.Content);
        Assert.Equal(SessionChatMessageType.AgentResponse, msg.MessageType);
        Assert.Equal(5, msg.SequenceNumber);
        Assert.Equal("tutor", msg.AgentType);
        Assert.Equal(0.92f, msg.Confidence);
        Assert.Equal("[{\"source\":\"rulebook\"}]", msg.CitationsJson);
        Assert.Equal(3, msg.TurnNumber);
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
        Assert.Equal("updated content", msg.Content);
        Assert.NotNull(msg.UpdatedAt);
    }

    [Fact]
    public void UpdateContent_TrimsWhitespace()
    {
        var msg = SessionChatMessage.CreateTextMessage(_sessionId, _senderId, "original", 1);
        msg.UpdateContent("  trimmed  ");
        Assert.Equal("trimmed", msg.Content);
    }

    [Fact]
    public void UpdateContent_SystemEvent_ThrowsInvalidOperationException()
    {
        var msg = SessionChatMessage.CreateSystemEvent(_sessionId, "event", 1);
        Assert.Throws<InvalidOperationException>(() => msg.UpdateContent("new content"));
    }

    [Fact]
    public void UpdateContent_AgentResponse_ThrowsInvalidOperationException()
    {
        var msg = SessionChatMessage.CreateAgentResponse(_sessionId, "answer", 1, "tutor");
        Assert.Throws<InvalidOperationException>(() => msg.UpdateContent("new content"));
    }

    [Fact]
    public void UpdateContent_EmptyContent_ThrowsArgumentException()
    {
        var msg = SessionChatMessage.CreateTextMessage(_sessionId, _senderId, "original", 1);
        Assert.Throws<ArgumentException>(() => msg.UpdateContent(""));
        Assert.Throws<ArgumentException>(() => msg.UpdateContent("   "));
    }

    [Fact]
    public void UpdateContent_ContentExceeds5000Chars_ThrowsArgumentException()
    {
        var msg = SessionChatMessage.CreateTextMessage(_sessionId, _senderId, "original", 1);
        var longContent = new string('A', 5001);
        Assert.Throws<ArgumentException>(() => msg.UpdateContent(longContent));
    }

    [Fact]
    public void SoftDelete_SetsDeletedFlags()
    {
        // Arrange
        var msg = SessionChatMessage.CreateTextMessage(_sessionId, _senderId, "content", 1);

        // Act
        msg.SoftDelete();

        // Assert
        Assert.True(msg.IsDeleted);
        Assert.NotNull(msg.DeletedAt);
        Assert.NotNull(msg.UpdatedAt);
    }
}
