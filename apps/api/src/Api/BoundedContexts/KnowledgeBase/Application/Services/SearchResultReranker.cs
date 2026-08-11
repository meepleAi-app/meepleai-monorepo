using System.Globalization;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.Observability;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Issue #2708: applies cross-encoder reranking to hybrid-search results on the
/// <c>/agents/qa</c> and <c>/agents/qa/stream</c> paths before the chunks are handed to the LLM.
///
/// Rationale: multilingual-e5 cosine similarities are compressed (relevant ≈0.85, off-topic ≈0.74),
/// so the raw RRF ordering under-discriminates. These paths retrieve a WIDER candidate pool and use
/// the cross-encoder (BGE-reranker-v2-m3) to select the final top-K by semantic relevance, improving
/// precision without raising MinScore (which would only make the agent more evasive).
///
/// Graceful degradation: if the reranker is unavailable or returns nothing usable, the raw top-K
/// order is used, and a retrieval-fallback metric is recorded (mirrors
/// <see cref="RagPromptAssemblyService"/>'s reranking fallback).
/// </summary>
internal static class SearchResultReranker
{
    /// <summary>
    /// Reranks <see cref="SearchResultDto"/> results (the /agents/qa[/stream] path).
    /// </summary>
    public static Task<List<SearchResultDto>> RerankAsync(
        ICrossEncoderReranker reranker,
        string query,
        IReadOnlyList<SearchResultDto> results,
        int topK,
        ILogger logger,
        CancellationToken cancellationToken)
        => RerankByIndexAsync(
            reranker, query, results, topK,
            static r => r.TextContent, static r => r.RelevanceScore,
            logger, cancellationToken);

    /// <summary>
    /// Reranks <see cref="HybridSearchResult"/> results (Slice B: the playground path). Uses the
    /// same index-keyed core so it returns the ORIGINAL objects reordered/trimmed — every field
    /// (notably <c>ChunkIndex</c>, which a <see cref="SearchResultDto"/> conversion would drop)
    /// survives untouched.
    /// </summary>
    public static Task<List<HybridSearchResult>> RerankAsync(
        ICrossEncoderReranker reranker,
        string query,
        IReadOnlyList<HybridSearchResult> results,
        int topK,
        ILogger logger,
        CancellationToken cancellationToken)
        => RerankByIndexAsync(
            reranker, query, results, topK,
            static r => r.Content, static r => (double)r.HybridScore,
            logger, cancellationToken);

    /// <summary>
    /// Index-keyed reranking core shared by both public overloads. The list index is the stable key
    /// that maps a <see cref="RerankChunk"/> back to its original element, so the reranker only needs
    /// a text + score projection and all other fields pass through opaquely.
    /// </summary>
    private static async Task<List<T>> RerankByIndexAsync<T>(
        ICrossEncoderReranker reranker,
        string query,
        IReadOnlyList<T> results,
        int topK,
        Func<T, string> contentSelector,
        Func<T, double> scoreSelector,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reranker);
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(logger);

        // Nothing to reorder — skip the network hop entirely.
        if (results.Count <= 1)
        {
            return results.Take(topK).ToList();
        }

        var rerankChunks = results
            .Select((r, i) => new RerankChunk(
                Id: i.ToString(CultureInfo.InvariantCulture),
                Content: contentSelector(r),
                OriginalScore: scoreSelector(r)))
            .ToList();

        try
        {
            var rerankResult = await reranker
                .RerankAsync(query, rerankChunks, topK, cancellationToken)
                .ConfigureAwait(false);

            return MapRerankedIndices(results, rerankResult.Chunks, topK);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cross-encoder reranking failed; using raw top {TopK} results", topK);
            MeepleAiMetrics.RecordRetrievalFallback(MeepleAiMetrics.RagFallbackTypes.Reranker);
            return results.Take(topK).ToList();
        }
    }

    /// <summary>
    /// Pure index-mapping: maps each reranked chunk's list-index id back to its original element,
    /// dropping unparseable/out-of-range ids. If nothing maps (a reranker that renamed/dropped all
    /// ids), falls back to the raw top-K so the context is never blanked.
    /// </summary>
    internal static List<T> MapRerankedIndices<T>(
        IReadOnlyList<T> results,
        IReadOnlyList<RerankedChunk> reranked,
        int topK)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(reranked);

        var mapped = reranked
            .Select(c => int.TryParse(c.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx) ? idx : -1)
            .Where(idx => idx >= 0 && idx < results.Count)
            .Select(idx => results[idx])
            .ToList();

        // Defensive: a reranker that returns no mappable chunks must not blank the context.
        return mapped.Count > 0 ? mapped : results.Take(topK).ToList();
    }
}
