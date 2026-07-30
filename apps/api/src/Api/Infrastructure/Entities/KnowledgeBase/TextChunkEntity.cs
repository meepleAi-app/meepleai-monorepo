using Api.Infrastructure.Entities.SharedGameCatalog;
using System.ComponentModel.DataAnnotations.Schema;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;

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

    // SP-A (#3405, epic #3403): char offsets del chunk nel testo ricostruito dal chunker,
    // per il grounding/highlight della citazione. Nullable: le righe indicizzate prima di
    // questa feature non hanno il dato (backfill via re-index — il valore è derivabile da
    // StructuredElementsJson/ExtractedText). Reference frame: content ricostruito della
    // sezione (non l'ExtractedText grezzo né il PDF originale).
    public int? CharStart { get; set; }
    public int? CharEnd { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Issue #730: Chunk hierarchy fields (heading_path derivation)
    public string? Heading { get; set; }
    public Guid? ParentChunkId { get; set; }
    // Defaults below are migration fill values for pre-existing rows.
    // Real values are populated from ChunkPayload by AdvancedChunkingService on new ingestions.
    public short Level { get; set; } = 1;
    public string ElementType { get; set; } = "NarrativeText";

    /// <summary>
    /// #2311 BE-1 — denormalized counter of distinct assistant messages that cited this chunk.
    /// Forward-looking metric (start-from-0 per DEC-D2): incremented post-SaveChanges by
    /// <see cref="Api.BoundedContexts.KnowledgeBase.Application.Commands.IncrementChunkUsageCountsCommand"/>
    /// inside <see cref="Api.BoundedContexts.KnowledgeBase.Application.Commands.ChatWithSessionAgentCommandHandler"/>.
    /// Surfaced via <see cref="Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunks.KbChunkSummaryDto.UsedInChats"/>.
    /// </summary>
    public int UsageCount { get; set; }

    // PostgreSQL full-text search vector (automatically maintained by trigger)
    // This column is populated by the tsvector_update_text_chunks trigger
    [Column("search_vector")]
    public string? SearchVector { get; set; }

    // Phase D — RAG role-aware: multi-label role classification per chunk
    public GameBookRole RoleTags { get; private set; } = GameBookRole.None;

    public void AssignRoleTags(GameBookRole tags)
    {
        RoleTags = tags;
    }

    // Navigation properties
    public SharedGameEntity Game { get; set; } = default!;
    public PdfDocumentEntity PdfDocument { get; set; } = default!;
}
