using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Entity representing a message in a chat thread.
/// Changed from ValueObject to Entity to support update/delete operations (Issue #1184).
/// </summary>
public sealed class ChatMessage
{
    public Guid Id { get; private set; }
    public string Content { get; private set; }
    public string Role { get; private set; } // "user" or "assistant"
    public DateTime Timestamp { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public Guid? DeletedByUserId { get; private set; }
    public bool IsInvalidated { get; private set; }
    public int SequenceNumber { get; private set; }

    // Known roles
    public static readonly string UserRole = "user";
    public static readonly string AssistantRole = "assistant";

    // Private constructor for EF Core
    private ChatMessage()
    {
        Content = string.Empty;
        Role = string.Empty;
    }

    public ChatMessage(string content, string role, int sequenceNumber, DateTime? timestamp = null, Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ValidationException("Message content cannot be empty");

        var trimmed = content.Trim();
        if (trimmed.Length > 10000)
            throw new ValidationException("Message content cannot exceed 10,000 characters");

        if (!string.Equals(role, UserRole, StringComparison.Ordinal) && !string.Equals(role, AssistantRole, StringComparison.Ordinal))
            throw new ValidationException($"Role must be '{UserRole}' or '{AssistantRole}'");

        Id = id ?? Guid.NewGuid();
        Content = trimmed;
        Role = role;
        SequenceNumber = sequenceNumber;
        Timestamp = timestamp ?? DateTime.UtcNow;
        IsDeleted = false;
        IsInvalidated = false;
    }

    public bool IsUserMessage => string.Equals(Role, UserRole, StringComparison.Ordinal);
    public bool IsAssistantMessage => string.Equals(Role, AssistantRole, StringComparison.Ordinal);

    /// <summary>
    /// Updates the message content. Only user messages can be edited.
    /// </summary>
    public void UpdateContent(string newContent)
    {
        if (!IsUserMessage)
            throw new InvalidOperationException("Only user messages can be edited");

        if (IsDeleted)
            throw new InvalidOperationException("Cannot edit deleted message");

        if (string.IsNullOrWhiteSpace(newContent))
            throw new ValidationException("Message content cannot be empty");

        var trimmed = newContent.Trim();
        if (trimmed.Length > 10000)
            throw new ValidationException("Message content cannot exceed 10,000 characters");

        Content = trimmed;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Soft-deletes the message.
    /// </summary>
    public void Delete(Guid deletedByUserId)
    {
        if (IsDeleted)
            return; // Idempotent

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedByUserId = deletedByUserId;
    }

    /// <summary>
    /// Marks the message as invalidated (for AI responses after user message edit/delete).
    /// </summary>
    public void Invalidate()
    {
        if (!IsAssistantMessage)
            throw new InvalidOperationException("Only assistant messages can be invalidated");

        IsInvalidated = true;
    }

    public override string ToString() => $"[{Role}] {Content.Substring(0, Math.Min(50, Content.Length))}...";
}
