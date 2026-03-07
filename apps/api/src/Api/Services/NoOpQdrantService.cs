namespace Api.Services;

/// <summary>
/// No-op implementation of IQdrantService.
/// Qdrant has been replaced by pgvector (PgVectorStoreAdapter).
/// This stub prevents DI resolution failures for handlers that depend on IQdrantService.
/// All search/index operations return empty/success results.
/// </summary>
internal sealed class NoOpQdrantService : IQdrantService
{
    public Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<IndexResult> IndexDocumentChunksAsync(
        string gameId, string pdfId, List<DocumentChunk> chunks,
        CancellationToken cancellationToken = default)
        => Task.FromResult(IndexResult.CreateSuccess(0));

    public Task<IndexResult> IndexChunksWithMetadataAsync(
        Dictionary<string, string> metadata, List<DocumentChunk> chunks,
        CancellationToken cancellationToken = default)
        => Task.FromResult(IndexResult.CreateSuccess(0));

    public Task<SearchResult> SearchAsync(
        string gameId, float[] queryEmbedding, int limit = 5,
        IReadOnlyList<string>? documentIds = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(SearchResult.CreateSuccess(new List<SearchResultItem>()));

    public Task<SearchResult> SearchByCategoryAsync(
        string category, float[] queryEmbedding, int limit = 5,
        CancellationToken cancellationToken = default)
        => Task.FromResult(SearchResult.CreateSuccess(new List<SearchResultItem>()));

    public Task<bool> DeleteDocumentAsync(string pdfId, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<bool> DeleteByCategoryAsync(string category, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<IndexResult> IndexDocumentChunksAsync(
        string gameId, string pdfId, List<DocumentChunk> chunks, string language,
        CancellationToken cancellationToken = default)
        => Task.FromResult(IndexResult.CreateSuccess(0));

    public Task<SearchResult> SearchAsync(
        string gameId, float[] queryEmbedding, string language, int limit = 5,
        IReadOnlyList<string>? documentIds = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(SearchResult.CreateSuccess(new List<SearchResultItem>()));
}

/// <summary>
/// No-op implementation of IPrivateQdrantService.
/// Qdrant has been replaced by pgvector (PgVectorStoreAdapter).
/// </summary>
internal sealed class NoOpPrivateQdrantService : IPrivateQdrantService
{
    public Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<IndexResult> IndexPrivateAsync(
        Guid userId, Guid gameId, Guid pdfId, Guid libraryEntryId,
        List<DocumentChunk> chunks, CancellationToken cancellationToken = default)
        => Task.FromResult(IndexResult.CreateSuccess(0));

    public Task<SearchResult> SearchPrivateAsync(
        Guid userId, Guid gameId, float[] queryEmbedding, int limit = 5,
        CancellationToken cancellationToken = default)
        => Task.FromResult(SearchResult.CreateSuccess(new List<SearchResultItem>()));

    public Task<SearchResult> SearchPrivateAsync(
        Guid userId, Guid gameId, float[] queryEmbedding,
        IReadOnlyList<Guid>? pdfIds, int limit = 5,
        CancellationToken cancellationToken = default)
        => Task.FromResult(SearchResult.CreateSuccess(new List<SearchResultItem>()));

    public Task<bool> DeletePrivateByPdfIdAsync(
        Guid userId, Guid pdfId, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<bool> DeleteAllUserVectorsAsync(
        Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}
