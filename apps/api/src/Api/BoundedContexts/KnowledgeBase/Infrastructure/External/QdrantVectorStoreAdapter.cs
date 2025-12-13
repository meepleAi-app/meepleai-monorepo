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
        IReadOnlyList<Guid>? documentIds = null,
        CancellationToken cancellationToken = default)
    {
        // Issue #2141: Convert Guid documentIds to string[] for Qdrant native filtering
        var documentIdStrings = documentIds?.Select(id => id.ToString()).ToList();

        // Call QdrantService with native document filtering
        var searchResult = await _qdrantService.SearchAsync(
            gameId.ToString(),
            queryVector.Values.ToArray(),
            topK,
            documentIdStrings,
            cancellationToken).ConfigureAwait(false);

        if (!searchResult.Success)
        {
            _logger.LogError("Qdrant search failed: {Error}", searchResult.ErrorMessage);
            return new List<Embedding>();
        }

        // DIAGNOSTIC: Log Qdrant results (already filtered natively)
        _logger.LogInformation(
            "Qdrant returned {TotalResults} results for gameId={GameId} with native document filter. MinScore threshold={MinScore}",
            searchResult.Results.Count, gameId, minScore);

        foreach (var r in searchResult.Results)
        {
            _logger.LogInformation(
                "  - Score={Score:F4}, PdfId={PdfId}, Text={TextPreview}...",
                r.Score, r.PdfId, r.Text.Substring(0, Math.Min(50, r.Text.Length)));
        }

        // Issue #2141: Only apply minScore filter now (document filter done by Qdrant natively)
        var filteredResults = searchResult.Results
            .Where(r => r.Score >= minScore)
            .ToList();

        _logger.LogInformation(
            "After minScore filter: {FilteredCount}/{TotalCount} results passed (threshold={MinScore})",
            filteredResults.Count, searchResult.Results.Count, minScore);

        var embeddings = filteredResults
            .Select((result, index) => KnowledgeBaseMappers.CreateEmbeddingFromQdrant(
                embeddingId: Guid.NewGuid(),
                vectorDocumentId: Guid.Parse(result.PdfId),
                textContent: result.Text,
                pageNumber: result.Page,
                vectorArray: new float[768], // Placeholder vector (search results don't include actual vectors)
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
        var firstEmbedding = embeddings[0];
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
            cancellationToken).ConfigureAwait(false);

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
            cancellationToken).ConfigureAwait(false);

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