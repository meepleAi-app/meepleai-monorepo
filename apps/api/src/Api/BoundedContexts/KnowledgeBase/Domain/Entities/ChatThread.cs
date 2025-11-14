using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// ChatThread aggregate root representing a conversation thread with Q&A history.
/// </summary>
public sealed class ChatThread : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public Guid? GameId { get; private set; }
    public string? Title { get; private set; }
    public ThreadStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime LastMessageAt { get; private set; }

    private readonly List<ChatMessage> _messages = new();
    public IReadOnlyList<ChatMessage> Messages => _messages.AsReadOnly();

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private ChatThread() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new chat thread.
    /// </summary>
    public ChatThread(
        Guid id,
        Guid userId,
        Guid? gameId = null,
        string? title = null) : base(id)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        UserId = userId;
        GameId = gameId;
        Title = title?.Trim();
        Status = ThreadStatus.Active;
        CreatedAt = DateTime.UtcNow;
        LastMessageAt = CreatedAt;

        // TODO: Add domain event ChatThreadCreated
    }

    /// <summary>
    /// Adds a message to the thread.
    /// </summary>
    public void AddMessage(ChatMessage message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        if (Status.IsClosed)
            throw new InvalidOperationException("Cannot add message to closed thread");

        _messages.Add(message);
        LastMessageAt = message.Timestamp;

        // TODO: Add domain event MessageAdded
    }

    /// <summary>
    /// Adds a user message to the thread.
    /// </summary>
    public void AddUserMessage(string content)
    {
        var message = new ChatMessage(content, ChatMessage.UserRole);
        AddMessage(message);
    }

    /// <summary>
    /// Adds an assistant message to the thread.
    /// </summary>
    public void AddAssistantMessage(string content)
    {
        var message = new ChatMessage(content, ChatMessage.AssistantRole);
        AddMessage(message);
    }

    /// <summary>
    /// Sets or updates the thread title.
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
    /// Gets total message count.
    /// </summary>
    public int MessageCount => _messages.Count;

    /// <summary>
    /// Checks if thread is empty.
    /// </summary>
    public bool IsEmpty => _messages.Count == 0;

    /// <summary>
    /// Gets the last message in the thread.
    /// </summary>
    public ChatMessage? LastMessage => _messages.Count > 0 ? _messages[^1] : null;

    /// <summary>
    /// Closes the thread, preventing further messages.
    /// </summary>
    public void CloseThread()
    {
        if (Status.IsClosed)
            throw new InvalidOperationException("Thread is already closed");

        Status = ThreadStatus.Closed;

        // TODO: Add domain event ThreadClosed
    }

    /// <summary>
    /// Reopens a closed thread.
    /// </summary>
    public void ReopenThread()
    {
        if (Status.IsActive)
            throw new InvalidOperationException("Thread is already active");

        Status = ThreadStatus.Active;

        // TODO: Add domain event ThreadReopened
    }
}
