namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO for chat thread information.
/// </summary>
public record ChatThreadDto(
    Guid Id,
    Guid? GameId,
    string? Title,
    DateTime CreatedAt,
    DateTime LastMessageAt,
    int MessageCount,
    List<ChatMessageDto> Messages
);

/// <summary>
/// DTO for chat message.
/// </summary>
public record ChatMessageDto(
    string Content,
    string Role,
    DateTime Timestamp
);

/// <summary>
/// DTO for creating a chat thread.
/// </summary>
public record CreateChatThreadRequest(
    Guid? GameId = null,
    string? Title = null,
    string? InitialMessage = null
);

/// <summary>
/// DTO for adding a message to thread.
/// </summary>
public record AddMessageRequest(
    string Content,
    string Role
);
