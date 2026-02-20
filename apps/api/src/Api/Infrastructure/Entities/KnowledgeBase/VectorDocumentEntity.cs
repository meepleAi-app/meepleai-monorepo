using Api.Infrastructure.Entities.SharedGameCatalog;

namespace Api.Infrastructure.Entities;

/// <summary>
/// Tracks which PDF documents have been indexed in Qdrant.
/// Either GameId (user library) or SharedGameId (admin-owned content) must be set.
/// </summary>
public class VectorDocumentEntity
{
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid Id { get; set; } = Guid.NewGuid();
    // DDD-PHASE2: Converted to Guid for domain alignment. Nullable — either GameId or SharedGameId must be set.
    public Guid? GameId { get; set; }
    // Issue #4921: Admin-owned content. Either GameId or SharedGameId must be set.
    public Guid? SharedGameId { get; set; }
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid PdfDocumentId { get; set; } // FK to PdfDocumentEntity

    public int ChunkCount { get; set; }
    public int TotalCharacters { get; set; }
    public string IndexingStatus { get; set; } = "pending"; // pending, processing, completed, failed
    public DateTime? IndexedAt { get; set; }
    public string? IndexingError { get; set; }

    // Embeddings metadata
    // NOTE: These defaults will be overridden by PdfIndexingService with actual values from EmbeddingService
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public int EmbeddingDimensions { get; set; } = 768;

    // Domain metadata (JSON string for flexible metadata storage)
    public string? Metadata { get; set; }

    // Navigation properties
    public GameEntity? Game { get; set; }
    public SharedGameEntity? SharedGame { get; set; }
    public PdfDocumentEntity PdfDocument { get; set; } = default!;
}
