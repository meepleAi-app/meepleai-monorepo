using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get paginated admin chat session list with optional filtering.
/// Issue #4917: Admin chat history real data.
/// </summary>
internal record GetAdminChatSessionsQuery(
    string? AgentType = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<AdminChatSessionsResult>;

/// <summary>
/// Result type for admin chat sessions query.
/// </summary>
internal record AdminChatSessionsResult(
    IReadOnlyList<AdminChatSessionDto> Sessions,
    int Total,
    int Page,
    int PageSize
);
