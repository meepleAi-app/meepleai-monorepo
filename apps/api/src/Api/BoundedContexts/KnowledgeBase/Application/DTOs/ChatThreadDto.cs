namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO for chat thread information.
/// </summary>
public record ChatThreadDto(
    Guid Id,
    Guid UserId,
    Guid? GameId,
    string? Title,
    string Status,
    DateTime CreatedAt,
    DateTime LastMessageAt,
    int MessageCount,
    List<ChatMessageDto> Messages
);

/// <summary>
/// DTO for chat message.
/// Enhanced to support update/delete operations (Issue #1184).
/// </summary>
public record ChatMessageDto(
    Guid Id,
    string Content,
    string Role,
    DateTime Timestamp,
    int SequenceNumber,
    DateTime? UpdatedAt = null,
    bool IsDeleted = false,
    DateTime? DeletedAt = null,
    Guid? DeletedByUserId = null,
    bool IsInvalidated = false
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
