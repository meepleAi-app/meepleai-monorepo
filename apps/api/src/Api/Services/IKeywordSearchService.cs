using Api.BoundedContexts.GameManagement.Domain.ValueObjects;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.Services;

/// <summary>
/// Interface for PostgreSQL full-text keyword search service.
/// Part of AI-14 hybrid search implementation for BM25-style keyword matching.
/// ADR-016 Phase 3: Supports Italian (meepleai_italian) and English FTS configurations.
/// </summary>
internal interface IKeywordSearchService
{
    /// <summary>
    /// Searches text chunks using PostgreSQL full-text search (tsvector + ts_rank_cd).
    /// </summary>
    /// <param name="query">Search query (supports phrase search with quotes)</param>
    /// <param name="gameId">Game ID to filter results</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="phraseSearch">Enable exact phrase matching with proximity operators</param>
    /// <param name="boostTerms">Retained for observability/forward-compat only; NOT currently applied to the tsquery. In-query ":A"/":B" weighting was removed because it matched nothing on the un-weighted search_vector (all lexemes are weight D). Re-introducing boost ranking requires setweight on the tsvector (migration + re-index).</param>
    /// <param name="language">Language code for FTS configuration: "en" → english, "it" → italian (default: "en", #2569 — matches content + pgvector path)</param>
    /// <param name="minScore">Minimum ts_rank_cd score threshold to filter low-relevance results (default: 0.0, no filtering)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of keyword search results with BM25-style relevance scores</returns>
    Task<List<KeywordSearchResult>> SearchAsync(
        string query,
        Guid gameId,
        int limit = 10,
        bool phraseSearch = false,
        List<string>? boostTerms = null,
        string language = "en",
        double minScore = 0.0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Titolo del gioco, per escluderne i token dai segnali che girano GIA' filtrati per gameId:
    /// la tsquery del braccio lessicale (#3769) e i termini di heading-match (#3768). Null quando
    /// il gioco non e' risolvibile — i chiamanti degradano al comportamento senza esclusione.
    /// </summary>
    Task<string?> ResolveGameTitleAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// #3338 WP1c: resolves the PostgreSQL FTS config (english/italian/…) the keyword arm uses for a
    /// game — detected from the game's dominant <c>pdf_documents.Language</c>, falling back to
    /// <paramref name="language"/>. Exposed so hybrid callers can expand heading-match terms with the
    /// same language's intent-synonym table (a "setup" query boosting a "preparazione" heading).
    /// </summary>
    Task<string> ResolveFtsConfigAsync(
        Guid gameId,
        string language = "en",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches PDF documents using PostgreSQL full-text search.
    /// Useful for document-level keyword matching.
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="gameId">Game ID to filter results</param>
    /// <param name="limit">Maximum number of results</param>
    /// <param name="language">Language code for FTS configuration: "en" → english, "it" → italian (default: "en", #2569 — matches content + pgvector path)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of PDF document search results</returns>
    Task<List<KeywordDocumentResult>> SearchDocumentsAsync(
        string query,
        Guid gameId,
        int limit = 10,
        string language = "en",
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from keyword search on text chunks with PostgreSQL ts_rank_cd scoring.
/// </summary>
internal record KeywordSearchResult
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
    /// </summary>
    public List<string> MatchedTerms { get; init; } = new List<string>();

    /// <summary>
    /// Phase D (D6): role classification of the text chunk (multi-label bitflag from text_chunks.role_tags).
    /// <see cref="GameBookRole.None"/> when the chunk has not been classified.
    /// </summary>
    public GameBookRole RoleTags { get; init; } = GameBookRole.None;

    /// <summary>#3270: chunk heading-path label for the heading-match boost (nullable).</summary>
    public string? Heading { get; init; }
}

/// <summary>
/// Result from keyword search on PDF documents.
/// </summary>
internal record KeywordDocumentResult
{
    public required string DocumentId { get; init; }
    public required string FileName { get; init; }
    public required Guid GameId { get; init; }
    public required float RelevanceScore { get; init; }
    public int? PageCount { get; init; }
}
