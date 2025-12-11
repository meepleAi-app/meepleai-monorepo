using System.Text;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;
using System.Globalization;

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

        AddDomainEvent(new ChatThreadCreatedEvent(id, gameId ?? Guid.Empty, userId));
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

        AddDomainEvent(new MessageAddedEvent(Id, message.Id, message.Role, message.Content.Length));
    }

    /// <summary>
    /// Adds a user message to the thread.
    /// </summary>
    public void AddUserMessage(string content)
    {
        var sequenceNumber = _messages.Count;
        var message = new ChatMessage(content, ChatMessage.UserRole, sequenceNumber);
        AddMessage(message);
    }

    /// <summary>
    /// Adds an assistant message to the thread.
    /// </summary>
    public void AddAssistantMessage(string content)
    {
        var sequenceNumber = _messages.Count;
        var message = new ChatMessage(content, ChatMessage.AssistantRole, sequenceNumber);
        AddMessage(message);
    }

    /// <summary>
    /// Updates a user message and invalidates subsequent AI responses.
    /// </summary>
    public void UpdateMessage(Guid messageId, string newContent, Guid userId)
    {
        var message = _messages.FirstOrDefault(m => m.Id == messageId);
        if (message == null)
            throw new InvalidOperationException($"Message {messageId} not found in thread");

        // Update the message (domain logic validates it's a user message)
        message.UpdateContent(newContent);

        // Invalidate all subsequent AI responses
        InvalidateMessagesAfter(message.SequenceNumber);

        AddDomainEvent(new MessageUpdatedEvent(Id, messageId, newContent.Length));
    }

    /// <summary>
    /// Soft-deletes a message and invalidates subsequent AI responses.
    /// </summary>
    public void DeleteMessage(Guid messageId, Guid deletedByUserId, bool isAdmin = false)
    {
        var message = _messages.FirstOrDefault(m => m.Id == messageId);
        if (message == null)
            throw new InvalidOperationException($"Message {messageId} not found in thread");

        // Authorization check (unless admin)
        if (!isAdmin && message.IsUserMessage)
        {
            // In a real system, we'd check message.CreatedByUserId == deletedByUserId
            // For now, we assume the handler validates this
        }

        // Delete the message
        message.Delete(deletedByUserId);

        // Invalidate all subsequent AI responses
        InvalidateMessagesAfter(message.SequenceNumber);

        AddDomainEvent(new MessageDeletedEvent(Id, messageId));
    }

    /// <summary>
    /// Invalidates all assistant messages after the given sequence number.
    /// </summary>
    private void InvalidateMessagesAfter(int sequenceNumber)
    {
        foreach (var msg in _messages.Where(m =>
            m.SequenceNumber > sequenceNumber &&
            m.IsAssistantMessage &&
            !m.IsInvalidated))
        {
            msg.Invalidate();
        }
    }

    /// <summary>
    /// Gets a message by ID.
    /// </summary>
    public ChatMessage? GetMessageById(Guid messageId)
    {
        return _messages.FirstOrDefault(m => m.Id == messageId);
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

        AddDomainEvent(new ThreadClosedEvent(Id, _messages.Count));
    }

    /// <summary>
    /// Reopens a closed thread.
    /// </summary>
    public void ReopenThread()
    {
        if (Status.IsActive)
            throw new InvalidOperationException("Thread is already active");

        Status = ThreadStatus.Active;

        AddDomainEvent(new ThreadReopenedEvent(Id));
    }

    /// <summary>
    /// Exports the chat thread in the specified format.
    /// </summary>
    /// <param name="format">The export format (JSON or Markdown).</param>
    /// <returns>Exported chat data as a value object.</returns>
    public ExportedChatData Export(ExportFormat format)
    {
        var content = format switch
        {
            ExportFormat.Json => ExportAsJson(),
            ExportFormat.Markdown => ExportAsMarkdown(),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Invalid export format")
        };

        return new ExportedChatData(format, content);
    }

    /// <summary>
    /// Exports thread as JSON.
    /// </summary>
    private string ExportAsJson()
    {
        var export = new
        {
            id = Id,
            userId = UserId,
            gameId = GameId,
            title = Title ?? "Untitled Chat",
            status = Status.Value,
            createdAt = CreatedAt,
            lastMessageAt = LastMessageAt,
            messageCount = MessageCount,
            messages = Messages.Select(m => new
            {
                role = m.Role,
                content = m.Content,
                timestamp = m.Timestamp
            }).ToList()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(export, options);
    }

    /// <summary>
    /// Exports thread as Markdown.
    /// </summary>
    private string ExportAsMarkdown()
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"# {Title ?? "Untitled Chat"}");
        sb.AppendLine();
        sb.AppendLine($"**Created:** {CreatedAt.ToString("yyyy-MM-dd HH:mm:ss UTC", CultureInfo.InvariantCulture)}  ");
        sb.AppendLine($"**Last Activity:** {LastMessageAt.ToString("yyyy-MM-dd HH:mm:ss UTC", CultureInfo.InvariantCulture)}  ");
        sb.AppendLine($"**Status:** {Status.Value}  ");
#pragma warning disable MA0011 // False positive: MessageCount is an int, no culture-sensitive formatting
        sb.AppendLine($"**Messages:** {MessageCount}");
#pragma warning restore MA0011
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Messages
        if (Messages.Count == 0)
        {
            sb.AppendLine("*No messages in this conversation.*");
        }
        else
        {
            foreach (var message in Messages)
            {
                var roleLabel = string.Equals(message.Role, ChatMessage.UserRole, StringComparison.Ordinal) ? "👤 User" : "🤖 Assistant";
                var timestamp = message.Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

                sb.AppendLine($"## {roleLabel} ({timestamp})");
                sb.AppendLine();
                sb.AppendLine(message.Content);
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }
        }

        // Footer
        sb.AppendLine($"*Exported on {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC", CultureInfo.InvariantCulture)}*");

        return sb.ToString();
    }
}
