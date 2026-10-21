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
    public Guid? AgentId { get; private set; }           // Custom agent (user-owned AgentDefinition)
    public string? AgentType { get; private set; }       // System agent type: auto|tutor|arbitro|decisore
    public string? AgentName { get; private set; }       // Display name for grouping (copied on create)
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
    /// Issue #4913: Added AgentId/AgentType/AgentName for grouped history.
    /// BE-3 #1590: gameName added so the ChatSessionCreatedEvent carries a complete
    /// payload for the activity rail without a secondary join.
    /// </summary>
    public ChatSession(
        Guid id,
        Guid userId,
        Guid gameId,
        string? title = null,
        Guid? userLibraryEntryId = null,
        Guid? agentSessionId = null,
        string? agentConfigJson = null,
        Guid? agentId = null,
        string? agentType = null,
        string? agentName = null,
        string? gameName = null) : base(id)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty", nameof(gameId));

        UserId = userId;
        GameId = gameId;
        UserLibraryEntryId = userLibraryEntryId;
        AgentSessionId = agentSessionId;
        AgentId = agentId;
        AgentType = agentType;
        AgentName = agentName;
        Title = title?.Trim();
        AgentConfigJson = agentConfigJson ?? "{}";
        CreatedAt = DateTime.UtcNow;
        LastMessageAt = CreatedAt;
        IsArchived = false;

        AddDomainEvent(new ChatSessionCreatedEvent(
            sessionId: id,
            userId: userId,
            gameId: gameId,
            gameName: gameName,
            agentId: agentId,
            agentName: agentName));
    }

    /// <summary>
    /// Ricostituisce una chat letta dal database, così com'è (#4613).
    /// </summary>
    /// <remarks>
    /// Solo per il repository. Il costruttore pubblico è quello di una chat NUOVA: solleva
    /// <see cref="ChatSessionCreatedEvent"/>, data la chat ad "adesso", toglie gli spazi dal titolo e rifiuta un
    /// utente o un gioco vuoti. Usato per ricostruire una riga, ogni aggregato letto portava già un «creata», e
    /// <c>UpdateAsync</c> lo consegnava al collector insieme all'evento vero: il feed attività mostrava una chat
    /// «creata» a ogni messaggio aggiunto.
    /// Nessun evento, quindi, e nessuna validazione: la riga è già stata scritta. <paramref name="messages"/> sono
    /// quelli che il chiamante ha caricato: un elenco può non portarli (la riga li ha tutti, l'aggregato solo quelli
    /// richiesti), e in quel caso l'aggregato non va salvato.
    /// </remarks>
    internal static ChatSession Reconstitute(
        Guid id,
        Guid userId,
        Guid gameId,
        Guid? userLibraryEntryId,
        Guid? agentSessionId,
        Guid? agentId,
        string? agentType,
        string? agentName,
        string? title,
        string agentConfigJson,
        DateTime createdAt,
        DateTime lastMessageAt,
        bool isArchived,
        IEnumerable<SessionChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(agentConfigJson);
        ArgumentNullException.ThrowIfNull(messages);

        var session = new ChatSession
        {
            Id = id,
            UserId = userId,
            GameId = gameId,
            UserLibraryEntryId = userLibraryEntryId,
            AgentSessionId = agentSessionId,
            AgentId = agentId,
            AgentType = agentType,
            AgentName = agentName,
            Title = title,
            AgentConfigJson = agentConfigJson,
            CreatedAt = createdAt,
            LastMessageAt = lastMessageAt,
            IsArchived = isArchived,
        };
        session._messages.AddRange(messages);
        return session;
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
