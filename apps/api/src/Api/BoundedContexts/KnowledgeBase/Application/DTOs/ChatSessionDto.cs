namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO representing a chat session with messages.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
public record ChatSessionDto(
    Guid Id,
    Guid UserId,
    Guid GameId,
    Guid? UserLibraryEntryId,
    Guid? AgentSessionId,
    string? Title,
    string AgentConfigJson,
    DateTime CreatedAt,
    DateTime LastMessageAt,
    bool IsArchived,
    int MessageCount,
    IReadOnlyList<ChatSessionMessageDto> Messages
);

/// <summary>
/// DTO representing a message in a chat session.
/// </summary>
public record ChatSessionMessageDto(
    Guid Id,
    string Role,
    string Content,
    DateTime Timestamp,
    int SequenceNumber,
    Dictionary<string, object>? Metadata
);

/// <summary>
/// DTO representing a chat session summary (without messages).
/// </summary>
public record ChatSessionSummaryDto(
    Guid Id,
    Guid UserId,
    Guid GameId,
    Guid? UserLibraryEntryId,
    string? Title,
    DateTime CreatedAt,
    DateTime LastMessageAt,
    int MessageCount,
    bool IsArchived
);

/// <summary>
/// DTO representing a paginated list of chat sessions.
/// </summary>
public record ChatSessionListDto(
    IReadOnlyList<ChatSessionSummaryDto> Sessions,
    int TotalCount,
    int Skip,
    int Take
);
