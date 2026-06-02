using System.Diagnostics;
using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KbQuality.Domain.Evaluation;
using Api.BoundedContexts.KbQuality.Domain.Goldset;

namespace Api.BoundedContexts.KbQuality.Application.Services;

/// <summary>
/// Orchestrates per-query search against the KB for each goldset Q&amp;A pair, accumulates
/// latencies + relevance hits, and produces a populated <see cref="EvaluationMetrics"/>.
/// Quality band is resolved via <see cref="IQualityBandResolver"/> using the
/// configured thresholds (D-G).
/// </summary>
public sealed class EvaluationExecutor(
    IKbSearchProvider search,
    IEvaluationMetricsCalculator calculator,
    IQualityBandResolver bands) : IEvaluationExecutor
{
    private const int TopK = 5;
    private const decimal CostPerQueryUsd = 0.001m;

    public async Task<EvaluationOutcome> ExecuteAsync(
        Guid docId,
        PdfDocSnapshot pdf,
        IReadOnlyList<GoldsetQaPair> goldset,
        long seed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(goldset);

        var latencies = new List<TimeSpan>();
        var queryResults = new List<QueryResult>();

        foreach (var pair in goldset)
        {
            var sw = Stopwatch.StartNew();
            var result = await search
                .SearchAsync(docId, pair.Question, TopK, cancellationToken)
                .ConfigureAwait(false);
            sw.Stop();

            latencies.Add(sw.Elapsed);
            var hits = result.RetrievedChunkIds.Select(id => id == pair.SourceChunkId).ToArray();
            queryResults.Add(new QueryResult(pair.Id, hits));
        }

        var ranking = calculator.Compute(queryResults);
        latencies.Sort();
        var (p50, p95) = ComputePercentiles(latencies);
        var queryCost = queryResults.Count * CostPerQueryUsd;

        var preliminaryMetrics = new EvaluationMetrics(
            Precision: new PrecisionMetrics(ranking.At1, ranking.At3, ranking.At5),
            Ranking: new RankingMetrics(ranking.Mrr),
            Latency: new LatencyMetrics(p50, p95),
            QueryCount: queryResults.Count,
            CostUsd: queryCost,
            QualityBand: QualityBand.Green);

        var band = bands.Resolve(preliminaryMetrics);
        var finalMetrics = preliminaryMetrics with { QualityBand = band };

        return new EvaluationOutcome(finalMetrics, queryCost);
    }

    private static (TimeSpan P50, TimeSpan P95) ComputePercentiles(IReadOnlyList<TimeSpan> sortedLatencies)
    {
        if (sortedLatencies.Count == 0) return (TimeSpan.Zero, TimeSpan.Zero);
        var p50Index = (int)(sortedLatencies.Count * 0.5);
        var p95Index = Math.Min((int)(sortedLatencies.Count * 0.95), sortedLatencies.Count - 1);
        return (sortedLatencies[p50Index], sortedLatencies[p95Index]);
    }
}
