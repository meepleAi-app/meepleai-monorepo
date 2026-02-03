using System.Text.Json;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value object representing a message in a chat session.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
/// <remarks>
/// Supports user, assistant, and system roles with optional metadata.
/// </remarks>
internal sealed class SessionChatMessage
{
    public Guid Id { get; private set; }
    public string Content { get; private set; }
    public string Role { get; private set; }
    public DateTime Timestamp { get; private set; }
    public int SequenceNumber { get; private set; }
    public string? MetadataJson { get; private set; }

    // Known roles
    public static readonly string UserRole = "user";
    public static readonly string AssistantRole = "assistant";
    public static readonly string SystemRole = "system";

    // Private constructor for EF Core / mapping
    private SessionChatMessage()
    {
        Content = string.Empty;
        Role = string.Empty;
    }

    public SessionChatMessage(
        string content,
        string role,
        int sequenceNumber,
        Dictionary<string, object>? metadata = null,
        DateTime? timestamp = null,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ValidationException("Message content cannot be empty");

        var trimmed = content.Trim();
        if (trimmed.Length > 50000) // Allow longer messages for assistant responses
            throw new ValidationException("Message content cannot exceed 50,000 characters");

        if (!IsValidRole(role))
            throw new ValidationException($"Role must be '{UserRole}', '{AssistantRole}', or '{SystemRole}'");

        Id = id ?? Guid.NewGuid();
        Content = trimmed;
        Role = role;
        SequenceNumber = sequenceNumber;
        Timestamp = timestamp ?? DateTime.UtcNow;
        MetadataJson = metadata != null ? JsonSerializer.Serialize(metadata) : null;
    }

    /// <summary>
    /// Creates a message from persistence data.
    /// </summary>
    public static SessionChatMessage FromPersistence(
        Guid id,
        string content,
        string role,
        int sequenceNumber,
        DateTime timestamp,
        string? metadataJson)
    {
        return new SessionChatMessage
        {
            Id = id,
            Content = content,
            Role = role,
            SequenceNumber = sequenceNumber,
            Timestamp = timestamp,
            MetadataJson = metadataJson
        };
    }

    public bool IsUserMessage => string.Equals(Role, UserRole, StringComparison.Ordinal);
    public bool IsAssistantMessage => string.Equals(Role, AssistantRole, StringComparison.Ordinal);
    public bool IsSystemMessage => string.Equals(Role, SystemRole, StringComparison.Ordinal);

    /// <summary>
    /// Gets the metadata as a dictionary.
    /// </summary>
    public Dictionary<string, object>? GetMetadata()
    {
        if (string.IsNullOrEmpty(MetadataJson))
            return null;

        return JsonSerializer.Deserialize<Dictionary<string, object>>(MetadataJson);
    }

    private static bool IsValidRole(string role) =>
        string.Equals(role, UserRole, StringComparison.Ordinal) ||
        string.Equals(role, AssistantRole, StringComparison.Ordinal) ||
        string.Equals(role, SystemRole, StringComparison.Ordinal);

    public override string ToString() => $"[{Role}] {Content[..Math.Min(50, Content.Length)]}...";
}
