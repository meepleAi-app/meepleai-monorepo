namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Represents a chat message within a game session.
/// Supports text messages, system events, and AI agent responses.
/// Issue #4760 - SessionMedia Entity + RAG Agent Integration + Shared Chat
/// </summary>
public class SessionChatMessage
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid? SenderId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public SessionChatMessageType MessageType { get; private set; }
    public int? TurnNumber { get; private set; }
    public int SequenceNumber { get; private set; }

    // Agent response metadata
    public string? AgentType { get; private set; }
    public float? Confidence { get; private set; }
    public string? CitationsJson { get; private set; }

    // Mention support
    public string? MentionsJson { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private SessionChatMessage() { }

    /// <summary>
    /// Creates a new text message from a participant.
    /// </summary>
    public static SessionChatMessage CreateTextMessage(
        Guid sessionId,
        Guid senderId,
        string content,
        int sequenceNumber,
        int? turnNumber = null,
        string? mentionsJson = null)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Session ID cannot be empty.", nameof(sessionId));

        if (senderId == Guid.Empty)
            throw new ArgumentException("Sender ID cannot be empty.", nameof(senderId));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Message content cannot be empty.", nameof(content));

        var trimmed = content.Trim();
        if (trimmed.Length > 5000)
            throw new ArgumentException("Message content cannot exceed 5,000 characters.", nameof(content));

        var now = DateTime.UtcNow;
        return new SessionChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            SenderId = senderId,
            Content = trimmed,
            MessageType = SessionChatMessageType.Text,
            SequenceNumber = sequenceNumber,
            TurnNumber = turnNumber,
            MentionsJson = mentionsJson,
            CreatedAt = now,
        };
    }

    /// <summary>
    /// Creates a system event message (e.g., "Alice joined", "Turn 3 started").
    /// </summary>
    public static SessionChatMessage CreateSystemEvent(
        Guid sessionId,
        string content,
        int sequenceNumber,
        int? turnNumber = null)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Session ID cannot be empty.", nameof(sessionId));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));

        var now = DateTime.UtcNow;
        return new SessionChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            SenderId = null,
            Content = content.Trim(),
            MessageType = SessionChatMessageType.SystemEvent,
            SequenceNumber = sequenceNumber,
            TurnNumber = turnNumber,
            CreatedAt = now,
        };
    }

    /// <summary>
    /// Creates an AI agent response message.
    /// </summary>
    public static SessionChatMessage CreateAgentResponse(
        Guid sessionId,
        string content,
        int sequenceNumber,
        string agentType,
        float? confidence = null,
        string? citationsJson = null,
        int? turnNumber = null)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Session ID cannot be empty.", nameof(sessionId));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));

        if (string.IsNullOrWhiteSpace(agentType))
            throw new ArgumentException("Agent type cannot be empty.", nameof(agentType));

        var now = DateTime.UtcNow;
        return new SessionChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            SenderId = null,
            Content = content.Trim(),
            MessageType = SessionChatMessageType.AgentResponse,
            SequenceNumber = sequenceNumber,
            TurnNumber = turnNumber,
            AgentType = agentType,
            Confidence = confidence,
            CitationsJson = citationsJson,
            CreatedAt = now,
        };
    }

    /// <summary>
    /// Updates the message content (text messages only).
    /// </summary>
    public void UpdateContent(string newContent)
    {
        if (MessageType != SessionChatMessageType.Text)
            throw new InvalidOperationException("Only text messages can be edited.");

        if (string.IsNullOrWhiteSpace(newContent))
            throw new ArgumentException("Message content cannot be empty.", nameof(newContent));

        var trimmed = newContent.Trim();
        if (trimmed.Length > 5000)
            throw new ArgumentException("Message content cannot exceed 5,000 characters.", nameof(newContent));

        Content = trimmed;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Soft deletes the message.
    /// </summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Types of chat messages in a session.
/// </summary>
public enum SessionChatMessageType
{
    Text = 0,
    SystemEvent = 1,
    AgentResponse = 2,
}
