using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Returns a paginated list of recent LLM request log entries with optional filtering.
/// Issue #5083: Admin usage page — recent requests table.
/// </summary>
internal sealed class GetRecentLlmRequestsQueryHandler
    : IRequestHandler<GetRecentLlmRequestsQuery, RecentLlmRequestsDto>
{
    private const int MaxPageSize = 100;

    private readonly ILlmRequestLogRepository _repository;

    public GetRecentLlmRequestsQueryHandler(ILlmRequestLogRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<RecentLlmRequestsDto> Handle(
        GetRecentLlmRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var (items, total) = await _repository.GetPagedAsync(
            source: request.Source,
            model: request.Model,
            from: request.From,
            until: request.To,
            successOnly: request.SuccessOnly,
            page: page,
            pageSize: pageSize,
            cancellationToken
        ).ConfigureAwait(false);

        var summaries = items.Select(x => new LlmRequestSummaryDto(
            Id: x.Id,
            RequestedAt: x.RequestedAt,
            ModelId: x.ModelId,
            Provider: x.Provider,
            Source: x.RequestSource,
            UserId: x.UserId,
            UserRole: x.UserRole,
            PromptTokens: x.PromptTokens,
            CompletionTokens: x.CompletionTokens,
            TotalTokens: x.TotalTokens,
            CostUsd: x.CostUsd,
            LatencyMs: x.LatencyMs,
            Success: x.Success,
            ErrorMessage: x.ErrorMessage,
            IsStreaming: x.IsStreaming,
            IsFreeModel: x.IsFreeModel
        )).ToList();

        var totalPages = (int)Math.Ceiling((double)total / pageSize);

        return new RecentLlmRequestsDto(
            Items: summaries,
            Total: total,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages
        );
    }
}
