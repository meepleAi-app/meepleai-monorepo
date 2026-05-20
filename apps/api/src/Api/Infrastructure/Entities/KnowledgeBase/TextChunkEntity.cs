using Api.Infrastructure.Entities.SharedGameCatalog;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities;

/// <summary>
/// Represents a text chunk extracted from a PDF document for hybrid search.
/// This table mirrors the data stored in pgvector vector database but enables PostgreSQL full-text search.
/// </summary>
public class TextChunkEntity
{
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid Id { get; set; } = Guid.NewGuid();
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid? GameId { get; set; }

    /// <summary>
    /// Cross-BC reference to SharedGameCatalog for hybrid search on shared games.
    /// When set, FTS queries match on this ID in addition to GameId.
    /// </summary>
    public Guid? SharedGameId { get; set; }

    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid PdfDocumentId { get; set; }
    public string Content { get; set; } = default!;
    public int ChunkIndex { get; set; }
    public int? PageNumber { get; set; }
    public int CharacterCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Issue #730: Chunk hierarchy fields (heading_path derivation)
    public string? Heading { get; set; }
    public Guid? ParentChunkId { get; set; }
    // Defaults below are migration fill values for pre-existing rows.
    // Real values are populated from ChunkPayload by AdvancedChunkingService on new ingestions.
    public short Level { get; set; } = 1;
    public string ElementType { get; set; } = "NarrativeText";

    // PostgreSQL full-text search vector (automatically maintained by trigger)
    // This column is populated by the tsvector_update_text_chunks trigger
    [Column("search_vector")]
    public string? SearchVector { get; set; }

    // Navigation properties
    public SharedGameEntity Game { get; set; } = default!;
    public PdfDocumentEntity PdfDocument { get; set; } = default!;
}
