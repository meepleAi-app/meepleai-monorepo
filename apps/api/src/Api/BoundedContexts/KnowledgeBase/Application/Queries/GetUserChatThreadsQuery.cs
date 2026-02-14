using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to retrieve user chat threads with filtering and pagination (Issue #4362).
/// Supports filtering by gameId, agentType, status, and search term.
/// </summary>
internal record GetUserChatThreadsQuery(
    Guid UserId,
    Guid? GameId = null,
    string? AgentType = null,
    string? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<ChatThreadListDto>;
