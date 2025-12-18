namespace Api.Services;

internal interface IQdrantService
{
    Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default);
    Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default);
    Task<IndexResult> IndexDocumentChunksAsync(
        string gameId,
        string pdfId,
        List<DocumentChunk> chunks,
        CancellationToken cancellationToken = default);
    Task<IndexResult> IndexChunksWithMetadataAsync(
        Dictionary<string, string> metadata,
        List<DocumentChunk> chunks,
        CancellationToken cancellationToken = default);
    Task<SearchResult> SearchAsync(
        string gameId,
        float[] queryEmbedding,
        int limit = 5,
        IReadOnlyList<string>? documentIds = null, // Issue #2141: Native Qdrant filtering
        CancellationToken cancellationToken = default);
    Task<SearchResult> SearchByCategoryAsync(
        string category,
        float[] queryEmbedding,
        int limit = 5,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteDocumentAsync(string pdfId, CancellationToken cancellationToken = default);
    Task<bool> DeleteByCategoryAsync(string category, CancellationToken cancellationToken = default);

    // AI-09: Multi-language support
    /// <summary>
    /// Index document chunks with language metadata
    /// </summary>
    Task<IndexResult> IndexDocumentChunksAsync(
        string gameId,
        string pdfId,
        List<DocumentChunk> chunks,
        string language,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for similar chunks filtered by game and language
    /// </summary>
    Task<SearchResult> SearchAsync(
        string gameId,
        float[] queryEmbedding,
        string language,
        int limit = 5,
        IReadOnlyList<string>? documentIds = null, // Issue #2141: Native Qdrant filtering
        CancellationToken cancellationToken = default);
}

