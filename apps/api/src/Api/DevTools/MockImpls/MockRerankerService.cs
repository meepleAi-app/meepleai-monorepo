using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;

namespace Api.DevTools.MockImpls;

/// <summary>
/// Deterministic mock of <see cref="ICrossEncoderReranker"/>.
/// Scores each chunk using Jaccard similarity between query tokens and document tokens.
/// Returns chunks sorted by score descending. No external HTTP calls.
/// Always reports itself as healthy.
/// </summary>
internal sealed class MockRerankerService : ICrossEncoderReranker
{
    private const string ModelName = "mock-bge-reranker-jaccard";

    private static readonly char[] TokenSeparators =
        { ' ', '.', ',', ';', '!', '?', '\t', '\n', '\r' };

    /// <inheritdoc />
    public Task<RerankResult> RerankAsync(
        string query,
        IReadOnlyList<RerankChunk> chunks,
        int? topK = null,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        var queryTokens = Tokenize(query);

        var ranked = chunks
            .Select(c => new RerankedChunk(
                Id: c.Id,
                Content: c.Content,
                OriginalScore: c.OriginalScore,
                RerankScore: Jaccard(queryTokens, Tokenize(c.Content)),
                Metadata: c.Metadata))
            .OrderByDescending(c => c.RerankScore)
            .ThenByDescending(c => c.OriginalScore)
            .ToList();

        IReadOnlyList<RerankedChunk> results = topK.HasValue
            ? ranked.Take(topK.Value).ToList()
            : ranked;

        var processingMs = (DateTime.UtcNow - started).TotalMilliseconds;
        return Task.FromResult(new RerankResult(results, ModelName, processingMs));
    }

    /// <inheritdoc />
    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    // ── private helpers ──────────────────────────────────────────────────────

    private static HashSet<string> Tokenize(string text)
        => new(
            text.ToLowerInvariant()
                .Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries),
            StringComparer.Ordinal);

    private static double Jaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 && b.Count == 0)
        {
            return 0.0;
        }

        var intersection = a.Intersect(b, StringComparer.Ordinal).Count();
        var union = a.Count + b.Count - intersection;
        return union == 0 ? 0.0 : (double)intersection / union;
    }
}
