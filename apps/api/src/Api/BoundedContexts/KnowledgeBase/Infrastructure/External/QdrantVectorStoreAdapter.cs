using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Mappers;
using Api.Services;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Concrete implementation of IQdrantVectorStoreAdapter.
/// Wraps existing QdrantService and maps to domain entities.
/// </summary>
public class QdrantVectorStoreAdapter : IQdrantVectorStoreAdapter
{
    private readonly IQdrantService _qdrantService;
    private readonly ILogger<QdrantVectorStoreAdapter> _logger;

    public QdrantVectorStoreAdapter(
        IQdrantService qdrantService,
        ILogger<QdrantVectorStoreAdapter> logger)
    {
        _qdrantService = qdrantService ?? throw new ArgumentNullException(nameof(qdrantService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<Embedding>> SearchAsync(
        Guid gameId,
        Vector queryVector,
        int topK,
        double minScore,
        CancellationToken cancellationToken = default)
    {
        // Call existing QdrantService with gameId and query vector
        var searchResult = await _qdrantService.SearchAsync(
            gameId.ToString(),
            queryVector.Values.ToArray(),
            topK,
            cancellationToken);

        if (!searchResult.Success)
        {
            _logger.LogError("Qdrant search failed: {Error}", searchResult.ErrorMessage);
            return new List<Embedding>();
        }

        // Map SearchResultItems to domain Embedding entities
        var embeddings = searchResult.Results
            .Where(r => r.Score >= minScore) // Apply min score filter
            .Select((result, index) => KnowledgeBaseMappers.CreateEmbeddingFromQdrant(
                embeddingId: Guid.NewGuid(),
                vectorDocumentId: Guid.Parse(result.PdfId),
                textContent: result.Text,
                pageNumber: result.Page,
                vectorArray: Array.Empty<float>(), // Vector not returned in search results
                model: "nomic-embed-text", // Default model
                chunkIndex: result.ChunkIndex
            ))
            .ToList();

        return embeddings;
    }

    public async Task IndexBatchAsync(
        List<Embedding> embeddings,
        CancellationToken cancellationToken = default)
    {
        if (embeddings == null || embeddings.Count == 0)
            return;

        // Group embeddings by VectorDocumentId (they should all be from same document)
        var firstEmbedding = embeddings.First();
        var gameId = "unknown"; // GameId not available in Embedding entity
        var pdfId = firstEmbedding.VectorDocumentId.ToString();

        // Convert domain Embeddings to DocumentChunks
        var chunks = embeddings.Select(e => new DocumentChunk
        {
            Text = e.TextContent,
            Embedding = e.Vector.Values.ToArray(),
            Page = e.PageNumber,
            CharStart = 0, // Not tracked in domain
            CharEnd = e.TextContent.Length
        }).ToList();

        // Index via QdrantService
        var result = await _qdrantService.IndexDocumentChunksAsync(
            gameId,
            pdfId,
            chunks,
            cancellationToken);

        if (!result.Success)
        {
            _logger.LogError("Qdrant indexing failed: {Error}", result.ErrorMessage);
            throw new InvalidOperationException($"Failed to index embeddings: {result.ErrorMessage}");
        }
    }

    public async Task DeleteByVectorDocumentIdAsync(
        Guid vectorDocumentId,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _qdrantService.DeleteDocumentAsync(
            vectorDocumentId.ToString(),
            cancellationToken);

        if (!deleted)
        {
            _logger.LogWarning(
                "Failed to delete vector document {VectorDocumentId} from Qdrant",
                vectorDocumentId);
        }
    }

    public async Task<bool> CollectionExistsAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        // QdrantService uses a single collection for all games
        // So we just check if the main collection exists
        return await _qdrantService.CollectionExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task EnsureCollectionExistsAsync(
        Guid gameId,
        int vectorDimension = 1536,
        CancellationToken cancellationToken = default)
    {
        // QdrantService manages collection creation globally
        await _qdrantService.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);
    }
}
