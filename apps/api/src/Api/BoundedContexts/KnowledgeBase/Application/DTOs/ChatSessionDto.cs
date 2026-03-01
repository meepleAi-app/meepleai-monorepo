namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO representing a chat session with messages.
/// Issue #3483: Chat Session Persistence Service.
/// Issue #4913: Added AgentId/AgentType/AgentName for grouped history.
/// </summary>
public record ChatSessionDto(
    Guid Id,
    Guid UserId,
    Guid GameId,
    Guid? UserLibraryEntryId,
    Guid? AgentSessionId,
    Guid? AgentId,
    string? AgentType,
    string? AgentName,
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
/// Issue #4913: Added AgentId/AgentType/AgentName for grouped history.
/// </summary>
public record ChatSessionSummaryDto(
    Guid Id,
    Guid UserId,
    Guid GameId,
    Guid? UserLibraryEntryId,
    Guid? AgentId,
    string? AgentType,
    string? AgentName,
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

/// <summary>
/// DTO representing the user's chat session tier limit info.
/// Issue #4913: Tier-based session limits.
/// </summary>
public record ChatSessionTierLimitDto(
    int Limit,     // Maximum allowed; 0 = unlimited
    int Used,      // Active (non-archived) session count
    string Tier    // User's tier name
);
