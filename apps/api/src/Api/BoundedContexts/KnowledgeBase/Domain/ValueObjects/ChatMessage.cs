using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value object representing a message in a chat thread.
/// </summary>
public sealed class ChatMessage : ValueObject
{
    public string Content { get; }
    public string Role { get; } // "user" or "assistant"
    public DateTime Timestamp { get; }

    // Known roles
    public static readonly string UserRole = "user";
    public static readonly string AssistantRole = "assistant";

    public ChatMessage(string content, string role, DateTime? timestamp = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ValidationException("Message content cannot be empty");

        var trimmed = content.Trim();
        if (trimmed.Length > 10000)
            throw new ValidationException("Message content cannot exceed 10,000 characters");

        if (role != UserRole && role != AssistantRole)
            throw new ValidationException($"Role must be '{UserRole}' or '{AssistantRole}'");

        Content = trimmed;
        Role = role;
        Timestamp = timestamp ?? DateTime.UtcNow;
    }

    public bool IsUserMessage => Role == UserRole;
    public bool IsAssistantMessage => Role == AssistantRole;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Content;
        yield return Role;
        yield return Timestamp;
    }

    public override string ToString() => $"[{Role}] {Content.Substring(0, Math.Min(50, Content.Length))}...";
}
