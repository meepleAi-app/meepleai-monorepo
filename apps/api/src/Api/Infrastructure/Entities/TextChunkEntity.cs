using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities;

/// <summary>
/// Represents a text chunk extracted from a PDF document for hybrid search.
/// This table mirrors the data stored in Qdrant vector database but enables PostgreSQL full-text search.
/// </summary>
public class TextChunkEntity
{
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid Id { get; set; } = Guid.NewGuid();
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid GameId { get; set; }
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid PdfDocumentId { get; set; }
    public string Content { get; set; } = default!;
    public int ChunkIndex { get; set; }
    public int? PageNumber { get; set; }
    public int CharacterCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // PostgreSQL full-text search vector (automatically maintained by trigger)
    // This column is populated by the tsvector_update_text_chunks trigger
    [Column("search_vector")]
    public string? SearchVector { get; set; }

    // Navigation properties
    public GameEntity Game { get; set; } = default!;
    public PdfDocumentEntity PdfDocument { get; set; } = default!;
}
