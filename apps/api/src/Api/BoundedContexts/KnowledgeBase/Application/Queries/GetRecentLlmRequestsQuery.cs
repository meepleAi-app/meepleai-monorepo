using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Returns a paginated, filtered list of recent LLM request log entries.
/// Issue #5083: Admin usage page — recent requests table.
/// </summary>
public sealed record GetRecentLlmRequestsQuery(
    string? Source = null,
    string? Model = null,
    DateTime? From = null,
    DateTime? To = null,
    bool? SuccessOnly = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<RecentLlmRequestsDto>;
