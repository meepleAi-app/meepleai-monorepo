using System.Globalization;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.Observability;
using Api.Services;
using Microsoft.Extensions.Logging;

#pragma warning disable MA0048 // File name must match type name - the reranker and what it reports (#4482)
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
    /// #4482: lo stesso reranking di <see cref="RerankAsync(ICrossEncoderReranker, string, IReadOnlyList{SearchResultDto}, int, ILogger, CancellationToken)"/>,
    /// detto per intero: quali candidati passano (per indice nella lista data), con il punteggio del
    /// cross-encoder, e se il cross-encoder ha lavorato davvero. Lo legge la sonda di recupero: un
    /// ripiego sui primi della fusione, sul servizio vivo, altrimenti si leggerebbe come una scelta
    /// del reranker.
    /// </summary>
    public static Task<RerankOutcome> RerankIndicesAsync(
        ICrossEncoderReranker reranker,
        string query,
        IReadOnlyList<SearchResultDto> results,
        int topK,
        ILogger logger,
        CancellationToken cancellationToken)
        => RerankOutcomeAsync(
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
    /// #4168: reranks <see cref="MultiGameSearchResultItem"/> results (the cross-game path, which
    /// before the answer pipeline did not rerank at all). Same index-keyed core: the original items
    /// come back reordered/trimmed, with GameId and ChunkId intact for the deep-links.
    /// </summary>
    public static Task<List<MultiGameSearchResultItem>> RerankAsync(
        ICrossEncoderReranker reranker,
        string query,
        IReadOnlyList<MultiGameSearchResultItem> results,
        int topK,
        ILogger logger,
        CancellationToken cancellationToken)
        => RerankByIndexAsync(
            reranker, query, results, topK,
            static r => r.Content, static r => (double)r.HybridScore,
            logger, cancellationToken);

    /// <summary>
    /// Index-keyed reranking core shared by the public overloads. The list index is the stable key
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
        var outcome = await RerankOutcomeAsync(
            reranker, query, results, topK, contentSelector, scoreSelector, logger, cancellationToken).ConfigureAwait(false);
        return outcome.Picks.Select(p => results[p.Index]).ToList();
    }

    /// <summary>
    /// The reranking itself, as indices into <paramref name="results"/> with the cross-encoder score
    /// of each, and whether the cross-encoder did the choosing (#4482).
    /// </summary>
    private static async Task<RerankOutcome> RerankOutcomeAsync<T>(
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
            return new RerankOutcome(RawTopK(results.Count, topK), RerankStatus.Skipped);
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

            var picks = MapRerankedPicks(results.Count, rerankResult.Chunks);

            // Defensive: a reranker that returns no mappable chunks must not blank the context.
            return picks.Count > 0
                ? new RerankOutcome(picks, RerankStatus.Applied, rerankResult.Model)
                : new RerankOutcome(RawTopK(results.Count, topK), RerankStatus.FellBack, rerankResult.Model);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cross-encoder reranking failed; using raw top {TopK} results", topK);
            MeepleAiMetrics.RecordRetrievalFallback(MeepleAiMetrics.RagFallbackTypes.Reranker);
            return new RerankOutcome(RawTopK(results.Count, topK), RerankStatus.FellBack);
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

        var picks = MapRerankedPicks(results.Count, reranked);

        // Defensive: a reranker that returns no mappable chunks must not blank the context.
        return picks.Count > 0
            ? picks.Select(p => results[p.Index]).ToList()
            : results.Take(topK).ToList();
    }

    /// <summary>The reranked chunks whose list-index id maps back into a list of <paramref name="count"/>, in reranked order.</summary>
    private static List<RerankPick> MapRerankedPicks(int count, IReadOnlyList<RerankedChunk> reranked) =>
        reranked
            .Select(c => (Index: int.TryParse(c.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx) ? idx : -1, c.RerankScore))
            .Where(p => p.Index >= 0 && p.Index < count)
            .Select(p => new RerankPick(p.Index, p.RerankScore))
            .ToList();

    /// <summary>The first <paramref name="topK"/> of <paramref name="count"/> candidates, unscored: the fusion order.</summary>
    private static List<RerankPick> RawTopK(int count, int topK) =>
        Enumerable.Range(0, Math.Max(0, Math.Min(count, topK))).Select(i => new RerankPick(i, null)).ToList();
}

/// <summary>
/// #4482: what a reranking did — the candidates it kept, by index in the list it was given and in
/// final order, how it got there, and the model the reranker service says it used (null when the
/// service was not called or did not answer).
/// </summary>
internal sealed record RerankOutcome(IReadOnlyList<RerankPick> Picks, RerankStatus Status, string? Model = null);

/// <summary>
/// #4482: one candidate kept by a reranking: its index in the list given to the reranker, and the
/// cross-encoder score when the cross-encoder scored it (null on the fusion order).
/// </summary>
internal sealed record RerankPick(int Index, double? Score);

/// <summary>#4482: how the final passages of a retrieval were chosen.</summary>
internal enum RerankStatus
{
    /// <summary>The cross-encoder reordered the pool, and its order is the final one.</summary>
    Applied,

    /// <summary>One candidate or none: the cross-encoder is not called, and the fusion order stays.</summary>
    Skipped,

    /// <summary>The cross-encoder failed or returned nothing usable: the first of the fusion order, as without it.</summary>
    FellBack,

    /// <summary>The agent profile turns the reranker off: the first of the fusion order.</summary>
    Disabled,
}
