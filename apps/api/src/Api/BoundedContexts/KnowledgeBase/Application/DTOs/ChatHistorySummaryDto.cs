namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Lightweight DTO for chat history dashboard display (Issue #2026).
/// Optimized to avoid loading full message arrays.
/// </summary>
internal record ChatHistorySummaryDto(
    Guid Id,
    Guid? GameId,
    string? GameName,
    string? Title,
    string LastMessageContent,
    DateTime LastMessageAt,
    int MessageCount
);
