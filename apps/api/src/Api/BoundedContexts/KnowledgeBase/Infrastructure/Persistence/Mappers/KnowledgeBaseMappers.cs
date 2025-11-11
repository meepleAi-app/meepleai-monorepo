using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure.Entities;
using Api.Services;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Mappers;

/// <summary>
/// Mapping extensions between domain entities and persistence entities.
/// Handles impedance mismatch between domain (Guid) and persistence (Guid after Phase 2 migration).
/// </summary>
public static class KnowledgeBaseMappers
{
    /// <summary>
    /// Maps domain VectorDocument to persistence VectorDocumentEntity.
    /// </summary>
    public static VectorDocumentEntity ToEntity(this VectorDocument domain)
    {
        return new VectorDocumentEntity
        {
            Id = domain.Id,
            GameId = domain.GameId,
            PdfDocumentId = domain.PdfDocumentId,
            ChunkCount = domain.TotalChunks,
            TotalCharacters = 0, // Not tracked in domain
            IndexingStatus = "completed", // Simplified status mapping
            IndexedAt = domain.IndexedAt,
            IndexingError = null,
            EmbeddingModel = "nomic-embed-text", // Default model
            EmbeddingDimensions = 768 // Default dimensions
        };
    }

    /// <summary>
    /// Maps persistence VectorDocumentEntity to domain VectorDocument.
    /// </summary>
    public static VectorDocument ToDomain(this VectorDocumentEntity entity)
    {
        return new VectorDocument(
            id: entity.Id,
            gameId: entity.GameId,
            pdfDocumentId: entity.PdfDocumentId,
            language: "en", // Default language (not stored in entity)
            totalChunks: entity.ChunkCount
        );
    }

    /// <summary>
    /// Maps HybridSearchResult to domain SearchResult.
    /// </summary>
    public static Domain.Entities.SearchResult ToDomainSearchResult(
        this HybridSearchResult result,
        int rank)
    {
        // Parse PdfDocumentId as the VectorDocumentId (they are the same in this context)
        var vectorDocId = Guid.Parse(result.PdfDocumentId);
        var pageNum = result.PageNumber ?? 1; // Default to page 1 if null
        var score = (double)result.HybridScore; // Use HybridScore as relevance
        var searchMethod = result.Mode.ToString().ToLowerInvariant(); // Convert enum to string

        return new Domain.Entities.SearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: vectorDocId,
            textContent: result.Content,
            pageNumber: pageNum,
            relevanceScore: new Confidence(score),
            rank: rank,
            searchMethod: searchMethod
        );
    }

    /// <summary>
    /// Extracts float[] from EmbeddingResult.
    /// EmbeddingResult contains List<float[]>, we take the first one for single text queries.
    /// </summary>
    public static float[] ToFloatArray(this EmbeddingResult embeddingResult)
    {
        if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
            throw new InvalidOperationException(
                $"Cannot extract embedding: {embeddingResult.ErrorMessage ?? "No embeddings generated"}");

        return embeddingResult.Embeddings[0];
    }

    /// <summary>
    /// Creates domain Embedding from Qdrant search result data.
    /// Note: This is a simplified mapping - real Qdrant results would need proper deserialization.
    /// </summary>
    public static Embedding CreateEmbeddingFromQdrant(
        Guid embeddingId,
        Guid vectorDocumentId,
        string textContent,
        int pageNumber,
        float[] vectorArray,
        string model,
        int chunkIndex)
    {
        var vector = new Vector(vectorArray);
        return new Embedding(
            id: embeddingId,
            vectorDocumentId: vectorDocumentId,
            textContent: textContent,
            vector: vector,
            model: model,
            chunkIndex: chunkIndex,
            pageNumber: pageNumber
        );
    }

    /// <summary>
    /// Converts domain Embedding to Qdrant point format.
    /// Returns tuple with data needed for Qdrant indexing.
    /// </summary>
    public static (Guid id, float[] vector, Dictionary<string, object> payload) ToQdrantPoint(
        this Embedding embedding)
    {
        var payload = new Dictionary<string, object>
        {
            ["vector_document_id"] = embedding.VectorDocumentId.ToString(),
            ["text_content"] = embedding.TextContent,
            ["page_number"] = embedding.PageNumber,
            ["chunk_index"] = embedding.ChunkIndex
        };

        return (embedding.Id, embedding.Vector.Values.ToArray(), payload);
    }
}
