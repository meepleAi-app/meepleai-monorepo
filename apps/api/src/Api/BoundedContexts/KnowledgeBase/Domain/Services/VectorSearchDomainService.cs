using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for vector search operations.
/// Encapsulates semantic search logic using vector embeddings.
/// </summary>
public class VectorSearchDomainService
{
    /// <summary>
    /// Performs vector similarity search and returns ranked results.
    /// </summary>
    /// <param name="queryVector">Query embedding vector</param>
    /// <param name="candidateEmbeddings">Candidate embeddings to search</param>
    /// <param name="topK">Number of top results to return</param>
    /// <param name="minScore">Minimum relevance score threshold</param>
    /// <returns>Ranked search results</returns>
    public List<SearchResult> Search(
        Vector queryVector,
        List<Embedding> candidateEmbeddings,
        int topK,
        double minScore)
    {
        if (topK <= 0)
            throw new ArgumentException("TopK must be positive", nameof(topK));

        if (minScore < 0 || minScore > 1)
            throw new ArgumentException("MinScore must be between 0 and 1", nameof(minScore));

        // Calculate similarity for each candidate
        var scoredResults = candidateEmbeddings
            .Select((embedding, index) =>
            {
                var similarity = queryVector.CosineSimilarity(embedding.Vector);
                var confidence = new Confidence(similarity);

                return new
                {
                    Embedding = embedding,
                    Confidence = confidence,
                    Index = index
                };
            })
            .Where(r => r.Confidence.Value >= minScore)
            .OrderByDescending(r => r.Confidence.Value)
            .Take(topK)
            .ToList();

        // Convert to SearchResult entities
        var results = scoredResults.Select((r, rank) => new SearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: r.Embedding.VectorDocumentId,
            textContent: r.Embedding.TextContent,
            pageNumber: r.Embedding.PageNumber,
            relevanceScore: r.Confidence,
            rank: rank + 1,
            searchMethod: "vector"
        )).ToList();

        return results;
    }

    /// <summary>
    /// Filters search results by minimum score threshold.
    /// </summary>
    public List<SearchResult> FilterByScore(List<SearchResult> results, double minScore)
    {
        var threshold = new Confidence(minScore);
        return results.Where(r => r.RelevanceScore.Value >= threshold.Value).ToList();
    }

    /// <summary>
    /// Validates search parameters.
    /// </summary>
    public void ValidateSearchParameters(int topK, double minScore)
    {
        if (topK <= 0 || topK > 100)
            throw new ArgumentException("TopK must be between 1 and 100", nameof(topK));

        if (minScore < 0.0 || minScore > 1.0)
            throw new ArgumentException("MinScore must be between 0.0 and 1.0", nameof(minScore));
    }
}
