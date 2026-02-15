using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.RagExecution;

/// <summary>
/// Paginated query for RAG execution history with filters.
/// Issue #4458: RAG Execution History
/// </summary>
internal record GetRagExecutionsQuery(
    int Skip = 0,
    int Take = 20,
    string? Strategy = null,
    string? Status = null,
    int? MinLatencyMs = null,
    int? MaxLatencyMs = null,
    double? MinConfidence = null,
    DateTime? From = null,
    DateTime? To = null
) : IQuery<GetRagExecutionsResult>;

internal record GetRagExecutionsResult(
    IReadOnlyList<RagExecutionListDto> Items,
    int TotalCount);

internal record RagExecutionListDto(
    Guid Id,
    string Query,
    string? AgentName,
    string Strategy,
    string? Model,
    int TotalLatencyMs,
    int TotalTokens,
    decimal TotalCost,
    double? Confidence,
    bool CacheHit,
    string Status,
    DateTime CreatedAt);

internal sealed class GetRagExecutionsQueryHandler
    : IQueryHandler<GetRagExecutionsQuery, GetRagExecutionsResult>
{
    private readonly IRagExecutionRepository _repository;

    public GetRagExecutionsQueryHandler(IRagExecutionRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetRagExecutionsResult> Handle(
        GetRagExecutionsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _repository.GetPagedAsync(
            request.Skip,
            request.Take,
            request.Strategy,
            request.Status,
            request.MinLatencyMs,
            request.MaxLatencyMs,
            request.MinConfidence,
            request.From,
            request.To,
            cancellationToken).ConfigureAwait(false);

        var dtos = items.Select(e => new RagExecutionListDto(
            e.Id,
            e.Query.Length > 100 ? e.Query[..100] + "..." : e.Query,
            e.AgentName,
            e.Strategy,
            e.Model,
            e.TotalLatencyMs,
            e.TotalTokens,
            e.TotalCost,
            e.Confidence,
            e.CacheHit,
            e.Status,
            e.CreatedAt)).ToList();

        return new GetRagExecutionsResult(dtos, totalCount);
    }
}
