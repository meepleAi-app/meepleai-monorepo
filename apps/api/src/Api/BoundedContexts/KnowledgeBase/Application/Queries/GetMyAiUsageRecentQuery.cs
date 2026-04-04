using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get paginated recent AI requests (last 7 days only).
/// Issue #94: C3 Editor Self-Service AI Usage Page
/// </summary>
internal record GetMyAiUsageRecentQuery(
    Guid UserId,
    int Page = 1,
    int PageSize = 20
) : IQuery<AiUsageRecentDto>;
