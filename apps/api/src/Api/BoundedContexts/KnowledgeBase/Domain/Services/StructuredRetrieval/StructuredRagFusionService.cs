using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.StructuredRetrieval;

/// <summary>
/// Fuses structured retrieval results with vector search results using weighted RRF.
/// Structured results receive 1.5x weight in the fusion ranking.
/// Issue #5453: Structured RAG fusion.
/// </summary>
internal sealed class StructuredRagFusionService
{
    private const int RrfK = 60;
    private const double StructuredWeight = 1.5;
    private const double VectorWeight = 1.0;
    private const double LowConfidenceFallbackThreshold = 0.6;

    private readonly ILogger<StructuredRagFusionService> _logger;

    public StructuredRagFusionService(ILogger<StructuredRagFusionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Fuses structured and vector results into a unified ranked list.
    /// Returns the fused results along with contribution metadata for logging.
    /// </summary>
    public FusionResult Fuse(
        StructuredRetrievalResponse structuredResponse,
        List<SearchResult> vectorResults,
        int maxResults = 10)
    {
        // Case 1: High-confidence structured → bypass vector
        if (structuredResponse.ShouldBypassVector && structuredResponse.Results.Count > 0)
        {
            _logger.LogInformation(
                "High-confidence structured bypass: {Count} structured results (confidence {Conf:F2})",
                structuredResponse.Results.Count,
                structuredResponse.Results.Max(r => r.Confidence));

            var bypassResults = structuredResponse.Results
                .Take(maxResults)
                .Select((r, i) => ToSearchResult(r, i + 1))
                .ToList();

            return new FusionResult(
                Results: bypassResults,
                StructuredContributionPercent: 100.0,
                VectorContributionPercent: 0.0,
                FusionStrategy: "structured_bypass",
                Intent: structuredResponse.Classification.Intent);
        }

        // Case 2: Low-confidence structured → vector-only fallback
        if (structuredResponse.Results.Count == 0 ||
            structuredResponse.Classification.Confidence < LowConfidenceFallbackThreshold)
        {
            _logger.LogDebug(
                "Low-confidence structured fallback: using vector-only ({Confidence:F2})",
                structuredResponse.Classification.Confidence);

            return new FusionResult(
                Results: vectorResults.Take(maxResults).ToList(),
                StructuredContributionPercent: 0.0,
                VectorContributionPercent: 100.0,
                FusionStrategy: "vector_only",
                Intent: structuredResponse.Classification.Intent);
        }

        // Case 3: Weighted RRF fusion
        var fusedResults = FuseWithWeightedRrf(
            structuredResponse.Results, vectorResults, maxResults);

        var structuredCount = fusedResults.Count(r =>
            r.SearchMethod?.StartsWith("structured", StringComparison.Ordinal) == true);
        var totalCount = fusedResults.Count;
        var structuredPct = totalCount > 0 ? (double)structuredCount / totalCount * 100.0 : 0.0;

        _logger.LogInformation(
            "Weighted RRF fusion: {Total} results ({StructuredCount} structured, {VectorCount} vector), structured contribution {Pct:F0}%",
            totalCount, structuredCount, totalCount - structuredCount, structuredPct);

        return new FusionResult(
            Results: fusedResults,
            StructuredContributionPercent: structuredPct,
            VectorContributionPercent: 100.0 - structuredPct,
            FusionStrategy: "weighted_rrf",
            Intent: structuredResponse.Classification.Intent);
    }

    private List<SearchResult> FuseWithWeightedRrf(
        IReadOnlyList<StructuredRetrievalResult> structuredResults,
        List<SearchResult> vectorResults,
        int maxResults)
    {
        // Build a combined score dictionary
        // Key: a synthetic ID for each result
        var scoredItems = new List<(string Key, double Score, SearchResult Result)>();

        // Score structured results with 1.5x weight
        for (var i = 0; i < structuredResults.Count; i++)
        {
            var rank = i + 1;
            var rrfScore = StructuredWeight / (RrfK + rank);
            // Also incorporate the retrieval confidence as a boost
            var boostedScore = rrfScore * (1.0 + structuredResults[i].Confidence);

            var result = ToSearchResult(structuredResults[i], rank);
            scoredItems.Add(($"structured_{i}", boostedScore, result));
        }

        // Score vector results with 1.0x weight
        for (var i = 0; i < vectorResults.Count; i++)
        {
            var rank = vectorResults[i].Rank > 0 ? vectorResults[i].Rank : i + 1;
            var rrfScore = VectorWeight / (RrfK + rank);

            scoredItems.Add(($"vector_{i}", rrfScore, vectorResults[i]));
        }

        // Sort by score descending and take top results
        var fusedResults = scoredItems
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .Select((item, index) => new SearchResult(
                id: item.Result.Id,
                vectorDocumentId: item.Result.VectorDocumentId,
                textContent: item.Result.TextContent,
                pageNumber: item.Result.PageNumber,
                relevanceScore: new Confidence(NormalizeScore(item.Score)),
                rank: index + 1,
                searchMethod: item.Result.SearchMethod))
            .ToList();

        return fusedResults;
    }

    private static double NormalizeScore(double rrfScore)
    {
        // Scale to 0-1 range. With k=60, max single-source score is ~1.5/61 ≈ 0.0246
        // With confidence boost, max is ~0.049. Scale by 20x for reasonable range.
        return Math.Clamp(rrfScore * 20.0, 0.0, 1.0);
    }

    private static SearchResult ToSearchResult(StructuredRetrievalResult result, int rank)
    {
        return new SearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: result.SharedGameId, // Use game ID as document reference
            textContent: result.Content,
            pageNumber: 1, // Structured results don't have page numbers
            relevanceScore: new Confidence(result.Confidence),
            rank: rank,
            searchMethod: $"structured_{result.SourceField}");
    }
}

/// <summary>
/// Result of fusing structured + vector search results with contribution metadata.
/// Issue #5453: Structured RAG fusion.
/// </summary>
internal sealed record FusionResult(
    List<SearchResult> Results,
    double StructuredContributionPercent,
    double VectorContributionPercent,
    string FusionStrategy,
    StructuredQueryIntent Intent);
