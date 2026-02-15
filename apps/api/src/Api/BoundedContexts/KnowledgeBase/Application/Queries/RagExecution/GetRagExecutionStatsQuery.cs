using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.RagExecution;

/// <summary>
/// Aggregated stats for RAG executions.
/// Issue #4458: RAG Execution History
/// </summary>
internal record GetRagExecutionStatsQuery(
    DateTime? From = null,
    DateTime? To = null
) : IQuery<RagExecutionStatsDto>;

internal record RagExecutionStatsDto(
    int TotalExecutions,
    double AvgLatencyMs,
    double ErrorRate,
    double CacheHitRate,
    decimal TotalCost,
    double AvgConfidence);

internal sealed class GetRagExecutionStatsQueryHandler
    : IQueryHandler<GetRagExecutionStatsQuery, RagExecutionStatsDto>
{
    private readonly IRagExecutionRepository _repository;

    public GetRagExecutionStatsQueryHandler(IRagExecutionRepository repository)
    {
        _repository = repository;
    }

    public async Task<RagExecutionStatsDto> Handle(
        GetRagExecutionStatsQuery request, CancellationToken cancellationToken)
    {
        var stats = await _repository.GetStatsAsync(request.From, request.To, cancellationToken)
            .ConfigureAwait(false);

        return new RagExecutionStatsDto(
            stats.TotalExecutions,
            Math.Round(stats.AvgLatencyMs, 1),
            Math.Round(stats.ErrorRate, 4),
            Math.Round(stats.CacheHitRate, 4),
            stats.TotalCost,
            Math.Round(stats.AvgConfidence, 4));
    }
}
