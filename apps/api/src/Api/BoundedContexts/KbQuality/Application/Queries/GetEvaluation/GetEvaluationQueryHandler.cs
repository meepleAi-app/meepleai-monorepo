using Api.BoundedContexts.KbQuality.Infrastructure;
using MediatR;

namespace Api.BoundedContexts.KbQuality.Application.Queries.GetEvaluation;

/// <summary>
/// Loads the aggregate via <see cref="IEvaluationRepository.GetByIdAsync"/> and projects it
/// to <see cref="EvaluationDetailDto"/>. Cross-doc check: if the run exists but belongs to a
/// different doc than the URL claims, return <c>null</c> (mapped to 404) rather than leak
/// the existence of the run to a caller that shouldn't see it under that nested route.
/// </summary>
public sealed class GetEvaluationQueryHandler(IEvaluationRepository repository)
    : IRequestHandler<GetEvaluationQuery, EvaluationDetailDto?>
{
    public async Task<EvaluationDetailDto?> Handle(GetEvaluationQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var run = await repository.GetByIdAsync(request.EvaluationId, cancellationToken).ConfigureAwait(false);
        if (run is null || run.PdfDocumentId != request.DocId)
        {
            return null;
        }

        var metrics = run.Metrics is null
            ? null
            : new EvaluationMetricsDto(
                Precision: new PrecisionDto(run.Metrics.Precision.At1, run.Metrics.Precision.At3, run.Metrics.Precision.At5),
                Ranking: new RankingDto(run.Metrics.Ranking.Mrr),
                Latency: new LatencyDto(
                    P50Ms: (int)run.Metrics.Latency.P50.TotalMilliseconds,
                    P95Ms: (int)run.Metrics.Latency.P95.TotalMilliseconds),
                QueryCount: run.Metrics.QueryCount,
                CostUsd: run.Metrics.CostUsd,
                QualityBand: run.Metrics.QualityBand.ToString());

        return new EvaluationDetailDto(
            EvaluationId: run.Id,
            PdfDocumentId: run.PdfDocumentId,
            StartedAt: run.StartedAt,
            CompletedAt: run.CompletedAt,
            Status: run.Status.ToString(),
            GoldsetVersion: run.GoldsetVersion,
            GoldsetGenerationSeed: run.GoldsetGenerationSeed,
            Metrics: metrics,
            CostUsd: run.CostUsd,
            TriggeredByAdminId: run.TriggeredByAdminId,
            ErrorMessage: run.ErrorMessage);
    }
}
