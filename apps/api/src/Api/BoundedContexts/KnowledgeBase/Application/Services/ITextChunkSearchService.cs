namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Provides PostgreSQL-backed text chunk operations:
/// - Full-text search (keyword) for hybrid search
/// - Adjacent chunk retrieval for sentence window expansion
/// </summary>
internal interface ITextChunkSearchService
{
    /// <summary>
    /// Performs PostgreSQL full-text search on text_chunks table.
    /// Returns chunks matching the query, ranked by relevance.
    /// </summary>
    Task<List<TextChunkMatch>> FullTextSearchAsync(
        Guid gameId,
        string query,
        int limit,
        CancellationToken ct);

    /// <summary>
    /// Retrieves adjacent chunks (chunk_index ± radius) for the same document.
    /// Used for sentence window expansion.
    /// </summary>
    Task<List<TextChunkMatch>> GetAdjacentChunksAsync(
        Guid pdfDocumentId,
        int chunkIndex,
        int radius,
        CancellationToken ct);

    /// <summary>
    /// Searches RAPTOR hierarchical summaries for a game using keyword matching.
    /// Higher tree levels (broader summaries) are preferred.
    /// </summary>
    Task<List<TextChunkMatch>> SearchRaptorSummariesAsync(
        Guid gameId,
        string query,
        int topK,
        CancellationToken ct);
}

/// <summary>
/// Represents a text chunk result from PostgreSQL search or retrieval.
/// </summary>
internal sealed record TextChunkMatch(
    Guid PdfDocumentId,
    string Content,
    int ChunkIndex,
    int? PageNumber,
    float Rank);
