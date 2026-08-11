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
    /// Fuses vector and keyword search results via <see cref="HybridFusionCore"/> (weighted RRF +
    /// legend-demotion + role-boost), preserving each result's carried cosine as its
    /// <see cref="SearchResult.RelevanceScore"/> (issue #2712) — the hybrid score drives ORDER only.
    /// </summary>
    /// <param name="vectorResults">Results from vector search</param>
    /// <param name="keywordResults">Results from keyword search</param>
    /// <param name="rrfK">RRF constant (default 60)</param>
    /// <param name="queryRoleHint">Optional role hint used to boost matching chunks (default None)</param>
    /// <param name="queryTerms">#3270: normalized query terms (lowercased, len≥3) for the heading-match boost (default null = no-op)</param>
    /// <returns>Fused and re-ranked results</returns>
    public virtual List<SearchResult> FuseResults(
        List<SearchResult> vectorResults,
        List<SearchResult> keywordResults,
        int rrfK = DefaultRrfK,
        GameBookRole queryRoleHint = GameBookRole.None,
        IReadOnlyList<string>? queryTerms = null)
    {
        if (rrfK <= 0)
            throw new ArgumentException("RRF K must be positive", nameof(rrfK));

        var vectorArm = vectorResults
            .Select((r, i) => new FusionCandidate(GetChunkKey(r), r.TextContent, r.RoleTags, r.Heading, i + 1, (float)r.RelevanceScore.Value))
            .ToList();
        var keywordArm = keywordResults
            .Select((r, i) => new FusionCandidate(GetChunkKey(r), r.TextContent, r.RoleTags, r.Heading, i + 1, (float)r.RelevanceScore.Value))
            .ToList();

        var fused = HybridFusionCore.Fuse(vectorArm, keywordArm, new FusionOptions(0.7f, 0.3f, rrfK, queryRoleHint, queryTerms));

        var vByKey = vectorResults.ToLookup(GetChunkKey, StringComparer.Ordinal);
        var kByKey = keywordResults.ToLookup(GetChunkKey, StringComparer.Ordinal);

        return fused
            .Select(f =>
            {
                // Prefer the vector-arm original when the chunk was found by both arms.
                var original = vByKey[f.Key].FirstOrDefault() ?? kByKey[f.Key].First();
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
                    roleTags: f.RoleTags,
                    heading: f.Heading,
                    // SP-C (#3407): carry the region-grounding primitives from the (vector-preferred)
                    // original, else the fused result drops the bbox/char offsets before the citation.
                    boundingBoxesJson: original.BoundingBoxesJson,
                    charStart: original.CharStart,
                    charEnd: original.CharEnd);
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
