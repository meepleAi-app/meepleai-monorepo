using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// EF Core entity for the pgvector_embeddings table.
/// The table is created by PgVectorStoreAdapter.EnsureTableExistsAsync() via raw SQL.
/// This entity provides typed EF Core access for export/import operations (RAG data backup).
/// </summary>
[Table("pgvector_embeddings")]
public class PgVectorEmbeddingEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("vector_document_id")]
    public Guid VectorDocumentId { get; set; }

    [Required]
    [Column("game_id")]
    public Guid GameId { get; set; }

    [Required]
    [Column("text_content")]
    public string TextContent { get; set; } = string.Empty;

    [Required]
    [Column("vector", TypeName = "vector(768)")]
    public Vector Vector { get; set; } = null!;

    [Required]
    [Column("model")]
    public string Model { get; set; } = string.Empty;

    [Required]
    [Column("chunk_index")]
    public int ChunkIndex { get; set; }

    [Required]
    [Column("page_number")]
    public int PageNumber { get; set; }

    [Required]
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // search_vector is a GENERATED ALWAYS AS column — not mapped in EF Core.

    [Required]
    [Column("lang")]
    [MaxLength(5)]
    public string Lang { get; set; } = "en";

    [Column("source_chunk_id")]
    public Guid? SourceChunkId { get; set; }

    [Required]
    [Column("is_translation")]
    public bool IsTranslation { get; set; } = false;

    /// <summary>
    /// Issue #1391: denormalized copy of <c>text_chunks.role_tags</c> so semantic-mode
    /// pgvector queries can apply the role-match boost without joining the parent table.
    /// Sync invariant: SHOULD equal <c>text_chunks.role_tags</c> for the row identified by
    /// <see cref="SourceChunkId"/>. Maintained at write time by the ingestion pipeline and
    /// once globally by the AddRoleTagsToPgVectorEmbeddings migration backfill. There is
    /// no DB-level constraint nor event handler that propagates re-classifications, so
    /// late calls to <c>TextChunk.AssignRoleTags</c> after pgvector ingestion will leave
    /// this column stale until the next re-embed (VectorReembeddingService) runs. Tracked
    /// for a future <c>TextChunkRoleReclassifiedDomainEvent</c> follow-up.
    /// </summary>
    [Required]
    [Column("role_tags")]
    public int RoleTags { get; set; } = 0;
}
