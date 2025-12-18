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

        // Build dictionary of document ID -> RRF score
        var rrfScores = new Dictionary<Guid, double>();

        // Add scores from vector results
        foreach (var result in vectorResults)
        {
            var score = 1.0 / (rrfK + result.Rank);
            if (!rrfScores.TryGetValue(result.VectorDocumentId, out var existingScore))
                rrfScores[result.VectorDocumentId] = score;
            else
                rrfScores[result.VectorDocumentId] = existingScore + score;
        }

        // Add scores from keyword results
        foreach (var result in keywordResults)
        {
            var score = 1.0 / (rrfK + result.Rank);
            if (!rrfScores.TryGetValue(result.VectorDocumentId, out var existingScore))
                rrfScores[result.VectorDocumentId] = score;
            else
                rrfScores[result.VectorDocumentId] = existingScore + score;
        }

        // Combine all results and re-rank by RRF score
        var allResults = vectorResults.Concat(keywordResults)
            .GroupBy(r => r.VectorDocumentId)
            .Select(g => g.First()) // Take first occurrence of each document
            .ToList();

        // First, calculate RRF scores and sort by score
        var scoredResults = allResults
            .Select(result => new
            {
                Result = result,
                RrfScore = rrfScores[result.VectorDocumentId],
                NormalizedScore = NormalizeRrfScore(rrfScores[result.VectorDocumentId])
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
