using Api.BoundedContexts.KbQuality.Infrastructure;
using MediatR;

namespace Api.BoundedContexts.KbQuality.Application.Queries.ListEvaluations;

/// <summary>
/// Delegates pagination + count to <see cref="IEvaluationRepository.ListByDocAsync"/>
/// (which already enforces <c>ORDER BY StartedAt DESC</c> and AsNoTracking), then projects
/// each aggregate to the slim list-item DTO. Page is clamped to ≥ 1, page size to [1, 100].
/// </summary>
public sealed class ListEvaluationsQueryHandler(IEvaluationRepository repository)
    : IRequestHandler<ListEvaluationsQuery, PagedEvaluationsDto>
{
    public async Task<PagedEvaluationsDto> Handle(ListEvaluationsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (runs, total) = await repository
            .ListByDocAsync(request.DocId, page, pageSize, cancellationToken)
            .ConfigureAwait(false);

        var items = runs.Select(r => new EvaluationRunListItemDto(
            EvaluationId: r.Id,
            StartedAt: r.StartedAt,
            CompletedAt: r.CompletedAt,
            Status: r.Status.ToString(),
            GoldsetVersion: r.GoldsetVersion,
            PrecisionAt5: r.Metrics?.Precision.At5,
            Mrr: r.Metrics?.Ranking.Mrr,
            LatencyP95Ms: r.Metrics is null ? null : (int)r.Metrics.Latency.P95.TotalMilliseconds,
            CostUsd: r.CostUsd,
            QualityBand: r.Metrics?.QualityBand.ToString())).ToList();

        return new PagedEvaluationsDto(items, total, page, pageSize);
    }
}
