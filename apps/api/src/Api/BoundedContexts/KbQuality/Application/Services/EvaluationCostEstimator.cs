using Api.BoundedContexts.KbQuality.Application.Ports;

namespace Api.BoundedContexts.KbQuality.Application.Services;

/// <summary>
/// Pre-flight cost estimator for a per-doc eval run (D-H).
/// Cost model: $0.002 per chunk goldset generation (top-5 chunks) + $0.001 per query execution
/// (3 queries per top chunk = 15 queries). Final ~$0.025/eval for a 30-chunk doc.
/// </summary>
public sealed class EvaluationCostEstimator(IPdfDocumentReadModel pdf) : IEvaluationCostEstimator
{
    private const int GoldsetTopChunks = 5;
    private const int QueriesPerChunk = 3;
    private const decimal CostPerChunkUsd = 0.002m;
    private const decimal CostPerQueryUsd = 0.001m;

    public async Task<decimal> EstimateAsync(Guid docId, CancellationToken ct)
    {
        var snapshot = await pdf.GetSnapshotAsync(docId, ct).ConfigureAwait(false);
        if (snapshot is null) return 0m;

        var effectiveTopChunks = Math.Min(GoldsetTopChunks, snapshot.ChunkCount);
        var goldsetCost = effectiveTopChunks * CostPerChunkUsd;
        var queryCost = effectiveTopChunks * QueriesPerChunk * CostPerQueryUsd;
        return goldsetCost + queryCost;
    }
}
