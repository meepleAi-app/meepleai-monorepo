

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.Services;

/// <summary>
/// Interface for hybrid search combining vector similarity (Qdrant) with keyword matching (PostgreSQL).
/// Uses Reciprocal Rank Fusion (RRF) algorithm to merge and rank results.
/// Part of AI-14 implementation.
/// </summary>
public interface IHybridSearchService
{
    /// <summary>
    /// Performs hybrid search using specified search mode.
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="gameId">Game ID to filter results</param>
    /// <param name="mode">Search mode: Semantic (vector only), Keyword (full-text only), or Hybrid (combined)</param>
    /// <param name="limit">Maximum number of results</param>
    /// <param name="vectorWeight">Weight for vector search scores (default: 0.7)</param>
    /// <param name="keywordWeight">Weight for keyword search scores (default: 0.3)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Hybrid search results with RRF-fused scores</returns>
    Task<List<HybridSearchResult>> SearchAsync(
        string query,
        Guid gameId,
        SearchMode mode = SearchMode.Hybrid,
        int limit = 10,
        float vectorWeight = 0.7f,
        float keywordWeight = 0.3f,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Search modes for hybrid search.
/// </summary>
public enum SearchMode
{
    /// <summary>
    /// Vector similarity search only (semantic search via Qdrant embeddings).
    /// Best for: Natural language questions, conceptual queries.
    /// </summary>
    Semantic,

    /// <summary>
    /// Keyword search only (PostgreSQL full-text search via tsvector).
    /// Best for: Exact terminology, specific rule names, phrase matching.
    /// </summary>
    Keyword,

    /// <summary>
    /// Hybrid search combining vector and keyword results using RRF fusion.
    /// Best for: General queries benefiting from both semantic understanding and exact matching.
    /// Default mode.
    /// </summary>
    Hybrid
}

/// <summary>
/// Result from hybrid search with RRF-fused scores.
/// </summary>
public record HybridSearchResult
{
    public required string ChunkId { get; init; }
    public required string Content { get; init; }
    public required string PdfDocumentId { get; init; }
    public required Guid GameId { get; init; }
    public required int ChunkIndex { get; init; }
    public int? PageNumber { get; init; }

    /// <summary>
    /// Final hybrid score computed using Reciprocal Rank Fusion (RRF).
    /// Combines vector similarity rank and keyword relevance rank.
    /// Higher score = more relevant result.
    /// </summary>
    public required float HybridScore { get; init; }

    /// <summary>
    /// Vector similarity score from Qdrant (0-1 range, cosine similarity).
    /// Null if SearchMode.Keyword used.
    /// </summary>
    public float? VectorScore { get; init; }

    /// <summary>
    /// Keyword relevance score from PostgreSQL ts_rank_cd.
    /// Null if SearchMode.Semantic used.
    /// </summary>
    public float? KeywordScore { get; init; }

    /// <summary>
    /// Vector rank position in vector-only results (1-based).
    /// Used for RRF calculation.
    /// </summary>
    public int? VectorRank { get; init; }

    /// <summary>
    /// Keyword rank position in keyword-only results (1-based).
    /// Used for RRF calculation.
    /// </summary>
    public int? KeywordRank { get; init; }

    /// <summary>
    /// Terms matched by keyword search for frontend highlighting.
    /// </summary>
    public List<string> MatchedTerms { get; init; } = new();

    /// <summary>
    /// Search mode used to produce this result.
    /// </summary>
    public required SearchMode Mode { get; init; }
}
