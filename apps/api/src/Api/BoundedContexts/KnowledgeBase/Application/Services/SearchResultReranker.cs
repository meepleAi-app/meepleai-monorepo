using System.Globalization;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.Observability;
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
    public static async Task<List<SearchResultDto>> RerankAsync(
        ICrossEncoderReranker reranker,
        string query,
        IReadOnlyList<SearchResultDto> results,
        int topK,
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

        // The list index is the stable key mapping a RerankChunk back to its SearchResultDto.
        var rerankChunks = results
            .Select((r, i) => new RerankChunk(
                Id: i.ToString(CultureInfo.InvariantCulture),
                Content: r.TextContent,
                OriginalScore: r.RelevanceScore))
            .ToList();

        try
        {
            var rerankResult = await reranker
                .RerankAsync(query, rerankChunks, topK, cancellationToken)
                .ConfigureAwait(false);

            var reranked = rerankResult.Chunks
                .Select(c => int.TryParse(c.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx) ? idx : -1)
                .Where(idx => idx >= 0 && idx < results.Count)
                .Select(idx => results[idx])
                .ToList();

            // Defensive: a reranker that returns no mappable chunks must not blank the context.
            return reranked.Count > 0 ? reranked : results.Take(topK).ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cross-encoder reranking failed; using raw top {TopK} results", topK);
            MeepleAiMetrics.RecordRetrievalFallback(MeepleAiMetrics.RagFallbackTypes.Reranker);
            return results.Take(topK).ToList();
        }
    }
}
