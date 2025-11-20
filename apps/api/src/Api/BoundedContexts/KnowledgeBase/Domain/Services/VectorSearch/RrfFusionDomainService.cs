using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for Reciprocal Rank Fusion (RRF).
/// Combines results from multiple search methods (vector + keyword) into a unified ranking.
/// </summary>
public class RrfFusionDomainService
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
            if (rrfScores.ContainsKey(result.VectorDocumentId))
                rrfScores[result.VectorDocumentId] += score;
            else
                rrfScores[result.VectorDocumentId] = score;
        }

        // Add scores from keyword results
        foreach (var result in keywordResults)
        {
            var score = 1.0 / (rrfK + result.Rank);
            if (rrfScores.ContainsKey(result.VectorDocumentId))
                rrfScores[result.VectorDocumentId] += score;
            else
                rrfScores[result.VectorDocumentId] = score;
        }

        // Combine all results and re-rank by RRF score
        var allResults = vectorResults.Concat(keywordResults)
            .GroupBy(r => r.VectorDocumentId)
            .Select(g => g.First()) // Take first occurrence of each document
            .ToList();

        var fusedResults = allResults
            .Select(result =>
            {
                var rrfScore = rrfScores[result.VectorDocumentId];
                var normalizedScore = NormalizeRrfScore(rrfScore);
                var confidence = new Confidence(normalizedScore);

                return new SearchResult(
                    id: Guid.NewGuid(),
                    vectorDocumentId: result.VectorDocumentId,
                    textContent: result.TextContent,
                    pageNumber: result.PageNumber,
                    relevanceScore: confidence,
                    rank: 0, // Will be set after sorting
                    searchMethod: "hybrid"
                );
            })
            .OrderByDescending(r => r.RelevanceScore.Value)
            .Select((r, index) =>
            {
                // Update rank after sorting
                return new SearchResult(
                    id: r.Id,
                    vectorDocumentId: r.VectorDocumentId,
                    textContent: r.TextContent,
                    pageNumber: r.PageNumber,
                    relevanceScore: r.RelevanceScore,
                    rank: index + 1,
                    searchMethod: "hybrid"
                );
            })
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
