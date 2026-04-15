using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for Reciprocal Rank Fusion (RRF).
/// Combines results from multiple search methods (vector + keyword) into a unified ranking.
/// </summary>
internal class RrfFusionDomainService
{
    private const int DefaultRrfK = 60; // PERF-08: Standard RRF constant

    /// <summary>
    /// Fuses vector and keyword search results using Reciprocal Rank Fusion.
    /// RRF formula: score = sum(1 / (k + rank)) for each result list.
    /// </summary>
    /// <param name="vectorResults">Results from vector search</param>
    /// <param name="keywordResults">Results from keyword search</param>
    /// <param name="rrfK">RRF constant (default 60)</param>
    /// <returns>Fused and re-ranked results</returns>
    public virtual List<SearchResult> FuseResults(
        List<SearchResult> vectorResults,
        List<SearchResult> keywordResults,
        int rrfK = DefaultRrfK)
    {
        if (rrfK <= 0)
            throw new ArgumentException("RRF K must be positive", nameof(rrfK));

        // Build dictionary of chunk key -> RRF score
        // Key is composite (documentId:page:contentHash) so each unique chunk is ranked independently.
        // Chunks from vector AND keyword search that share the same content fuse together correctly.
        var rrfScores = new Dictionary<string, double>(StringComparer.Ordinal);

        // Add scores from vector results
        foreach (var result in vectorResults)
        {
            var key = GetChunkKey(result);
            var score = 1.0 / (rrfK + result.Rank);
            if (!rrfScores.TryGetValue(key, out var existingScore))
                rrfScores[key] = score;
            else
                rrfScores[key] = existingScore + score;
        }

        // Add scores from keyword results
        foreach (var result in keywordResults)
        {
            var key = GetChunkKey(result);
            var score = 1.0 / (rrfK + result.Rank);
            if (!rrfScores.TryGetValue(key, out var existingScore))
                rrfScores[key] = score;
            else
                rrfScores[key] = existingScore + score;
        }

        // Combine all results and re-rank by RRF score
        var allResults = vectorResults.Concat(keywordResults)
            .GroupBy(r => GetChunkKey(r), StringComparer.Ordinal)
            .Select(g => g.First()) // Take first occurrence of each unique chunk
            .ToList();

        // First, calculate RRF scores and sort by score
        var scoredResults = allResults
            .Select(result => new
            {
                Result = result,
                RrfScore = rrfScores[GetChunkKey(result)],
                NormalizedScore = NormalizeRrfScore(rrfScores[GetChunkKey(result)])
            })
            .OrderByDescending(x => x.NormalizedScore)
            .ToList();

        // Then create SearchResult objects with correct rank (1-based, assigned after sorting)
        var fusedResults = scoredResults
            .Select((item, index) => new SearchResult(
                id: Guid.NewGuid(),
                vectorDocumentId: item.Result.VectorDocumentId,
                textContent: item.Result.TextContent,
                pageNumber: item.Result.PageNumber,
                relevanceScore: new Confidence(item.NormalizedScore),
                rank: index + 1, // Rank assigned after sorting
                searchMethod: "hybrid"
            ))
            .ToList();

        return fusedResults;
    }

    /// <summary>
    /// Generates a stable, chunk-level key for RRF fusion.
    /// Uses a composite of VectorDocumentId + PageNumber + TextContent hash so that:
    /// - the same chunk returned by both vector and keyword search fuses into one entry,
    /// - different chunks from the same document (same VectorDocumentId) remain separate.
    /// </summary>
    private static string GetChunkKey(SearchResult result)
    {
        // Unique per chunk: same content from vector and keyword search should fuse
        return $"{result.VectorDocumentId}:{result.PageNumber}:{result.TextContent.GetHashCode(StringComparison.Ordinal)}";
    }

    /// <summary>
    /// Normalizes RRF score to 0-1 range for confidence comparison.
    /// </summary>
    private double NormalizeRrfScore(double rrfScore)
    {
        // RRF scores typically range from 0 to ~0.03 (for k=60, rank=1)
        // Normalize to 0-1 range using a scaling factor
        var normalized = Math.Min(rrfScore * 30, 1.0); // Scale factor approximation
        return Math.Max(0.0, normalized);
    }

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
