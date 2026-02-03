using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// ChatSession aggregate root representing a chat conversation with persistence.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
/// <remarks>
/// Links user, game, and optionally UserLibraryEntry for chat history persistence.
/// Supports agent configuration storage and message history.
/// </remarks>
internal sealed class ChatSession : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public Guid GameId { get; private set; }
    public Guid? UserLibraryEntryId { get; private set; }
    public Guid? AgentSessionId { get; private set; }
    public string? Title { get; private set; }
    public string AgentConfigJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime LastMessageAt { get; private set; }
    public bool IsArchived { get; private set; }

    private readonly List<SessionChatMessage> _messages = new();
    public IReadOnlyList<SessionChatMessage> Messages => _messages.AsReadOnly();

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private ChatSession() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new chat session.
    /// </summary>
    public ChatSession(
        Guid id,
        Guid userId,
        Guid gameId,
        string? title = null,
        Guid? userLibraryEntryId = null,
        Guid? agentSessionId = null,
        string? agentConfigJson = null) : base(id)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty", nameof(gameId));

        UserId = userId;
        GameId = gameId;
        UserLibraryEntryId = userLibraryEntryId;
        AgentSessionId = agentSessionId;
        Title = title?.Trim();
        AgentConfigJson = agentConfigJson ?? "{}";
        CreatedAt = DateTime.UtcNow;
        LastMessageAt = CreatedAt;
        IsArchived = false;

        AddDomainEvent(new ChatSessionCreatedEvent(id, userId, gameId));
    }

    /// <summary>
    /// Adds a message to the session.
    /// </summary>
    public void AddMessage(SessionChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (IsArchived)
            throw new InvalidOperationException("Cannot add message to archived session");

        _messages.Add(message);
        LastMessageAt = message.Timestamp;

        AddDomainEvent(new ChatSessionMessageAddedEvent(Id, message.Id, message.Role));
    }

    /// <summary>
    /// Adds a user message to the session.
    /// </summary>
    public void AddUserMessage(string content, Dictionary<string, object>? metadata = null)
    {
        var sequenceNumber = _messages.Count;
        var message = new SessionChatMessage(content, SessionChatMessage.UserRole, sequenceNumber, metadata);
        AddMessage(message);
    }

    /// <summary>
    /// Adds an assistant message to the session.
    /// </summary>
    public void AddAssistantMessage(string content, Dictionary<string, object>? metadata = null)
    {
        var sequenceNumber = _messages.Count;
        var message = new SessionChatMessage(content, SessionChatMessage.AssistantRole, sequenceNumber, metadata);
        AddMessage(message);
    }

    /// <summary>
    /// Adds a system message to the session.
    /// </summary>
    public void AddSystemMessage(string content, Dictionary<string, object>? metadata = null)
    {
        var sequenceNumber = _messages.Count;
        var message = new SessionChatMessage(content, SessionChatMessage.SystemRole, sequenceNumber, metadata);
        AddMessage(message);
    }

    /// <summary>
    /// Sets or updates the session title.
    /// </summary>
    public void SetTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty", nameof(title));

        var trimmed = title.Trim();
        if (trimmed.Length > 200)
            throw new ArgumentException("Title cannot exceed 200 characters", nameof(title));

        Title = trimmed;
    }

    /// <summary>
    /// Updates the agent configuration.
    /// </summary>
    public void UpdateAgentConfig(string agentConfigJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentConfigJson);
        AgentConfigJson = agentConfigJson;
    }

    /// <summary>
    /// Links this session to an agent session.
    /// </summary>
    public void LinkToAgentSession(Guid agentSessionId)
    {
        if (agentSessionId == Guid.Empty)
            throw new ArgumentException("AgentSessionId cannot be empty", nameof(agentSessionId));

        AgentSessionId = agentSessionId;
    }

    /// <summary>
    /// Archives the session (soft close).
    /// </summary>
    public void Archive()
    {
        if (IsArchived)
            return; // Idempotent

        IsArchived = true;
        AddDomainEvent(new ChatSessionArchivedEvent(Id));
    }

    /// <summary>
    /// Unarchives the session.
    /// </summary>
    public void Unarchive()
    {
        if (!IsArchived)
            return; // Idempotent

        IsArchived = false;
    }

    /// <summary>
    /// Gets total message count.
    /// </summary>
    public int MessageCount => _messages.Count;

    /// <summary>
    /// Checks if session has messages.
    /// </summary>
    public bool HasMessages => _messages.Count > 0;

    /// <summary>
    /// Gets the last message in the session.
    /// </summary>
    public SessionChatMessage? LastMessage => _messages.Count > 0 ? _messages[^1] : null;
}
