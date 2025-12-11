

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.Services;

/// <summary>
/// Interface for PostgreSQL full-text keyword search service.
/// Part of AI-14 hybrid search implementation for BM25-style keyword matching.
/// ADR-016 Phase 3: Supports Italian (meepleai_italian) and English FTS configurations.
/// </summary>
public interface IKeywordSearchService
{
    /// <summary>
    /// Searches text chunks using PostgreSQL full-text search (tsvector + ts_rank_cd).
    /// </summary>
    /// <param name="query">Search query (supports phrase search with quotes)</param>
    /// <param name="gameId">Game ID to filter results</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="phraseSearch">Enable exact phrase matching with proximity operators</param>
    /// <param name="boostTerms">Optional list of terms to boost in ranking (e.g., game-specific terminology)</param>
    /// <param name="language">Language code for FTS configuration: "it" → meepleai_italian, "en" → english (default: "it")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of keyword search results with BM25-style relevance scores</returns>
    Task<List<KeywordSearchResult>> SearchAsync(
        string query,
        Guid gameId,
        int limit = 10,
        bool phraseSearch = false,
        List<string>? boostTerms = null,
        string language = "it",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches PDF documents using PostgreSQL full-text search.
    /// Useful for document-level keyword matching.
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="gameId">Game ID to filter results</param>
    /// <param name="limit">Maximum number of results</param>
    /// <param name="language">Language code for FTS configuration: "it" → meepleai_italian, "en" → english (default: "it")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of PDF document search results</returns>
    Task<List<KeywordDocumentResult>> SearchDocumentsAsync(
        string query,
        Guid gameId,
        int limit = 10,
        string language = "it",
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from keyword search on text chunks with PostgreSQL ts_rank_cd scoring.
/// </summary>
public record KeywordSearchResult
{
    public required string ChunkId { get; init; }
    public required string Content { get; init; }
    public required string PdfDocumentId { get; init; }
    public required Guid GameId { get; init; }
    public required int ChunkIndex { get; init; }
    public int? PageNumber { get; init; }

    /// <summary>
    /// PostgreSQL ts_rank_cd relevance score (higher = more relevant).
    /// Uses cover density ranking for better phrase match scoring.
    /// </summary>
    public required float RelevanceScore { get; init; }

    /// <summary>
    /// Matched search terms highlighted in the content.
    /// Used for frontend display with yellow highlighting.
    /// </summary>
    public List<string> MatchedTerms { get; init; } = new();
}

/// <summary>
/// Result from keyword search on PDF documents.
/// </summary>
public record KeywordDocumentResult
{
    public required string DocumentId { get; init; }
    public required string FileName { get; init; }
    public required Guid GameId { get; init; }
    public required float RelevanceScore { get; init; }
    public int? PageCount { get; init; }
}
