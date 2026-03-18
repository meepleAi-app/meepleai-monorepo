

#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs
namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO for chat thread information.
/// </summary>
internal record ChatThreadDto(
    Guid Id,
    Guid UserId,
    Guid? GameId,
    Guid? AgentId, // Issue #2030 - Track which agent was used
    string? Title,
    string Status,
    DateTime CreatedAt,
    DateTime LastMessageAt,
    int MessageCount,
    IReadOnlyList<ChatMessageDto> Messages,
    string? AgentType = null // Issue #4362
);

/// <summary>
/// DTO for chat message.
/// Enhanced to support update/delete operations (Issue #1184).
/// </summary>
internal record ChatMessageDto(
    Guid Id,
    string Content,
    string Role,
    DateTime Timestamp,
    int SequenceNumber,
    DateTime? UpdatedAt = null,
    bool IsDeleted = false,
    DateTime? DeletedAt = null,
    Guid? DeletedByUserId = null,
    bool IsInvalidated = false,
    // Issue #4362: Agent metadata
    string? AgentType = null,
    float? Confidence = null,
    string? CitationsJson = null,
    int? TokenCount = null
);

/// <summary>
/// DTO for creating a chat thread.
/// </summary>
internal record CreateChatThreadRequest(
    Guid? GameId = null,
    string? Title = null,
    string? InitialMessage = null,
    Guid? AgentId = null,
    string? AgentType = null, // Issue #4362
    List<Guid>? SelectedKnowledgeBaseIds = null  // VectorDocument IDs to use for RAG
);

/// <summary>
/// DTO for adding a message to thread.
/// </summary>
internal record AddMessageRequest(
    string Content,
    string Role
);

/// <summary>
/// DTO for a paginated list of chat threads (Issue #4362).
/// </summary>
internal record ChatThreadListDto(
    IReadOnlyList<ChatThreadDto> Threads,
    int TotalCount,
    int Page,
    int PageSize
);

/// <summary>
/// DTO for summary-level thread info (no messages) used in listings (Issue #4362).
/// </summary>
internal record ChatThreadSummaryDto(
    Guid Id,
    Guid UserId,
    Guid? GameId,
    Guid? AgentId,
    string? Title,
    string Status,
    DateTime CreatedAt,
    DateTime LastMessageAt,
    int MessageCount,
    string? AgentType = null
);
