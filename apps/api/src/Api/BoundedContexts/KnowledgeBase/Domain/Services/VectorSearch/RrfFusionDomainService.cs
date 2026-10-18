using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for Reciprocal Rank Fusion (RRF).
/// Combines results from multiple search methods (vector + keyword) into a unified ranking.
/// Issue #3270 (Task 5): this is the PRIMARY chat-path fusion (used by /agents/qa via
/// SearchQueryHandler). It now adapts to <see cref="HybridFusionCore"/> for scoring (weighted RRF +
/// legend-demotion + role-boost), the same core the admin playground's HybridSearchService uses.
/// </summary>
internal class RrfFusionDomainService
{
    private const int DefaultRrfK = FusionSignals.DefaultRrfK; // PERF-08: Standard RRF constant

    /// <summary>
    /// I pesi del seed del profilo dell'agente, che la fusione aveva scritti nel codice prima della
    /// fetta 5 di ADR-095 (#4168). Valgono per chi fonde senza un profilo: il recupero della risposta
    /// passa sempre quelli della versione pubblicata.
    /// </summary>
    internal const float DefaultVectorWeight = 0.7f;

    /// <inheritdoc cref="DefaultVectorWeight"/>
    internal const float DefaultKeywordWeight = 0.3f;

    /// <summary>
    /// Fuses vector and keyword search results via <see cref="HybridFusionCore"/> (weighted RRF +
    /// legend-demotion + role-boost), preserving each result's carried cosine as its
    /// <see cref="SearchResult.RelevanceScore"/> (issue #2712) — the hybrid score drives ORDER only.
    /// </summary>
    /// <param name="vectorResults">Results from vector search</param>
    /// <param name="keywordResults">Results from keyword search</param>
    /// <param name="rrfK">RRF constant (default 60)</param>
    /// <param name="queryRoleHint">Optional role hint used to boost matching chunks (default None)</param>
    /// <param name="queryTerms">#3270: normalized query terms (lowercased, len≥3) for the heading-match boost (default null = no-op)</param>
    /// <param name="vectorWeight">#4168: il peso del braccio vettoriale, dal profilo</param>
    /// <param name="keywordWeight">#4168: il peso del braccio lessicale, dal profilo</param>
    /// <returns>Fused and re-ranked results</returns>
    public virtual List<SearchResult> FuseResults(
        List<SearchResult> vectorResults,
        List<SearchResult> keywordResults,
        int rrfK = DefaultRrfK,
        GameBookRole queryRoleHint = GameBookRole.None,
        IReadOnlyList<string>? queryTerms = null,
        float vectorWeight = DefaultVectorWeight,
        float keywordWeight = DefaultKeywordWeight)
    {
        if (rrfK <= 0)
            throw new ArgumentException("RRF K must be positive", nameof(rrfK));

        // #4168 (5e-c-2): nei candidati il punteggio GREZZO del braccio, quando c'è (il ts_rank_cd
        // supera 1, e relevanceScore lo schiaccia). L'ordine non cambia: la fusione usa i ranghi, i
        // punteggi li porta soltanto, per chi fonde di nuovo fra giochi.
        var vectorArm = vectorResults
            .Select((r, i) => new FusionCandidate(GetChunkKey(r), r.TextContent, r.RoleTags, r.Heading, i + 1,
                r.Signals.VectorScore ?? (float)r.RelevanceScore.Value))
            .ToList();
        var keywordArm = keywordResults
            .Select((r, i) => new FusionCandidate(GetChunkKey(r), r.TextContent, r.RoleTags, r.Heading, i + 1,
                r.Signals.KeywordScore ?? (float)r.RelevanceScore.Value))
            .ToList();

        var fused = HybridFusionCore.Fuse(vectorArm, keywordArm, new FusionOptions(vectorWeight, keywordWeight, rrfK, queryRoleHint, queryTerms));

        var vByKey = vectorResults.ToLookup(GetChunkKey, StringComparer.Ordinal);
        var kByKey = keywordResults.ToLookup(GetChunkKey, StringComparer.Ordinal);

        return fused
            .Select(f =>
            {
                // Prefer the vector-arm original when the chunk was found by both arms.
                var fromVector = vByKey[f.Key].FirstOrDefault();
                var fromKeyword = kByKey[f.Key].FirstOrDefault();
                var original = fromVector ?? fromKeyword!;
                return new SearchResult(
                    id: Guid.NewGuid(),
                    vectorDocumentId: original.VectorDocumentId,
                    textContent: f.Content,
                    pageNumber: original.PageNumber,
                    // Issue #2712: preserve the original relevance signal (cosine similarity for
                    // vector results) as the RelevanceScore. The hybrid score computed by
                    // HybridFusionCore still drives the ORDER, but RelevanceScore must reflect
                    // semantic relevance — feeding a rank-based fused score into confidence made it
                    // degenerate.
                    relevanceScore: original.RelevanceScore,
                    rank: f.Rank,
                    searchMethod: "hybrid",
                    pdfDocumentId: original.PdfDocumentId,
                    chunkIndex: original.ChunkIndex,
                    // #4288: either arm can carry it; the vector one is null for a pre-SP2 embedding.
                    textChunkId: fromVector?.TextChunkId ?? fromKeyword?.TextChunkId,
                    roleTags: f.RoleTags,
                    heading: f.Heading,
                    // SP-C (#3407): carry the region-grounding primitives from the (vector-preferred)
                    // original, else the fused result drops the bbox/char offsets before the citation.
                    boundingBoxesJson: original.BoundingBoxesJson,
                    charStart: original.CharStart,
                    charEnd: original.CharEnd,
                    // #4168 (5e-c-2): i segnali della fusione, per chi fonde di nuovo fra giochi.
                    signals: new RetrievalSignals(
                        HybridScore: f.HybridScore,
                        VectorScore: f.VectorScore,
                        KeywordScore: f.KeywordScore,
                        VectorRank: f.VectorRank,
                        KeywordRank: f.KeywordRank,
                        Language: fromVector?.Signals.Language,
                        MatchedTerms: fromKeyword?.Signals.MatchedTerms));
            })
            .ToList();
    }

    /// <summary>
    /// Generates a stable, chunk-level key for fusion.
    /// Uses the unified chunk identity (PdfDocumentId + ChunkIndex, issue #3270) so that:
    /// - the same chunk returned by both vector and keyword search fuses into one entry,
    /// - different chunks from the same document remain separate.
    /// </summary>
    private static string GetChunkKey(SearchResult r) => $"{r.PdfDocumentId}_{r.ChunkIndex}";

    /// <summary>
    /// Calculates raw RRF score for a result at given rank.
    /// </summary>
    public virtual double CalculateRrfScore(int rank, int rrfK = DefaultRrfK)
    {
        if (rank <= 0)
            throw new ArgumentException("Rank must be positive", nameof(rank));

        return 1.0 / (rrfK + rank);
    }
}
