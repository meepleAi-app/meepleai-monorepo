using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to retrieve chat history summary for dashboard (Issue #2026).
/// Optimized version that doesn't load full message arrays.
/// </summary>
public record GetMyChatHistoryQuery(
    Guid UserId,
    int Skip = 0,
    int Take = 50
) : IQuery<GetMyChatHistoryResult>;

/// <summary>
/// Result containing chat summaries and total count for pagination.
/// </summary>
public record GetMyChatHistoryResult(
    IReadOnlyList<ChatHistorySummaryDto> Chats,
    int TotalCount
);
