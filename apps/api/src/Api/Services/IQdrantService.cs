namespace Api.Services;

public interface IQdrantService
{
    Task EnsureCollectionExistsAsync(CancellationToken ct = default);
    Task<IndexResult> IndexDocumentChunksAsync(
        string gameId,
        string pdfId,
        List<DocumentChunk> chunks,
        CancellationToken ct = default);
    Task<IndexResult> IndexChunksWithMetadataAsync(
        Dictionary<string, string> metadata,
        List<DocumentChunk> chunks,
        CancellationToken ct = default);
    Task<SearchResult> SearchAsync(
        string gameId,
        float[] queryEmbedding,
        int limit = 5,
        CancellationToken ct = default);
    Task<SearchResult> SearchByCategoryAsync(
        string category,
        float[] queryEmbedding,
        int limit = 5,
        CancellationToken ct = default);
    Task<bool> DeleteDocumentAsync(string pdfId, CancellationToken ct = default);
    Task<bool> DeleteByCategoryAsync(string category, CancellationToken ct = default);
}
