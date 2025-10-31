namespace Api.Infrastructure.Entities;

/// <summary>
/// Tracks which PDF documents have been indexed in Qdrant
/// </summary>
public class VectorDocumentEntity
{
    public string Id { get; set; } = default!;
    public string GameId { get; set; } = default!;
    public string PdfDocumentId { get; set; } = default!; // FK to PdfDocumentEntity

    public int ChunkCount { get; set; }
    public int TotalCharacters { get; set; }
    public string IndexingStatus { get; set; } = "pending"; // pending, processing, completed, failed
    public DateTime? IndexedAt { get; set; }
    public string? IndexingError { get; set; }

    // Embeddings metadata
    // NOTE: These defaults will be overridden by PdfIndexingService with actual values from EmbeddingService
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public int EmbeddingDimensions { get; set; } = 768;

    // Navigation properties
    public GameEntity Game { get; set; } = default!;
    public PdfDocumentEntity PdfDocument { get; set; } = default!;
}
