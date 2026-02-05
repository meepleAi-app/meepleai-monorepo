namespace Api.Services;

/// <summary>
/// Service interface for user-scoped private vector storage in Qdrant.
/// Issue #3651: Provides isolated vector search for private user PDFs.
/// Uses dedicated "private_rules" collection separate from shared "meepleai_documents".
/// </summary>
internal interface IPrivateQdrantService
{
    /// <summary>
    /// Ensure the private_rules collection exists with proper indexes.
    /// </summary>
    Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the private_rules collection exists.
    /// </summary>
    Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Index private document chunks with user-scoped metadata.
    /// All chunks are tagged with user_id for isolation.
    /// </summary>
    /// <param name="userId">Owner of the private PDF</param>
    /// <param name="gameId">Game this PDF belongs to</param>
    /// <param name="pdfId">Unique PDF document identifier</param>
    /// <param name="libraryEntryId">User library entry containing this PDF</param>
    /// <param name="chunks">Document chunks with embeddings</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<IndexResult> IndexPrivateAsync(
        Guid userId,
        Guid gameId,
        Guid pdfId,
        Guid libraryEntryId,
        List<DocumentChunk> chunks,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for similar chunks in user's private documents only.
    /// Always filters by user_id to ensure isolation.
    /// </summary>
    /// <param name="userId">User performing the search (only their documents are searched)</param>
    /// <param name="gameId">Game to search within</param>
    /// <param name="queryEmbedding">Vector embedding of the query</param>
    /// <param name="limit">Maximum results to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<SearchResult> SearchPrivateAsync(
        Guid userId,
        Guid gameId,
        float[] queryEmbedding,
        int limit = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search user's private documents with optional PDF filtering.
    /// </summary>
    /// <param name="userId">User performing the search</param>
    /// <param name="gameId">Game to search within</param>
    /// <param name="queryEmbedding">Vector embedding of the query</param>
    /// <param name="pdfIds">Optional: specific PDFs to search within</param>
    /// <param name="limit">Maximum results to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<SearchResult> SearchPrivateAsync(
        Guid userId,
        Guid gameId,
        float[] queryEmbedding,
        IReadOnlyList<Guid>? pdfIds,
        int limit = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete all vectors for a specific private PDF.
    /// Only deletes if the PDF belongs to the specified user.
    /// </summary>
    /// <param name="userId">Owner of the PDF (for safety validation)</param>
    /// <param name="pdfId">PDF document to delete vectors for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<bool> DeletePrivateByPdfIdAsync(
        Guid userId,
        Guid pdfId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete all vectors for a user (used when user deletes account).
    /// </summary>
    /// <param name="userId">User whose vectors to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<bool> DeleteAllUserVectorsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
