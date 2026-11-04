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

    /// <summary>
    /// #4384: la riga del passaggio <paramref name="chunkIndex"/>. È l'unico modo di costruire questa
    /// entità nel progetto Api (lo impone l'analizzatore MAI007), e chiede a quale <c>text_chunks</c> la
    /// riga appartiene.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Senza il collegamento la riga perde tre cose, in silenzio: il boost di ruolo
    /// (<see cref="RoleTags"/>, #1391), il boost di heading (#3270) e la localizzazione della citazione
    /// (#3407), che il braccio vettoriale legge con una <c>LEFT JOIN text_chunks ON tc."Id" =
    /// e.source_chunk_id</c>. <paramref name="sourceChunk"/> è <c>null</c> solo se i passaggi non sono
    /// stati salvati.
    /// </para>
    /// <para>
    /// Le righe scritte dall'entità di dominio <c>Embedding</c> con il COPY dell'adattatore pgvector non
    /// passano di qui: pipeline di Quartz, ri-embedding e indicizzatore delle tabelle passano
    /// <c>sourceChunkId</c>; i lotti di foto non hanno un passaggio per costruzione.
    /// </para>
    /// </remarks>
    public static PgVectorEmbeddingEntity ForChunk(
        Guid vectorDocumentId,
        Guid gameId,
        int chunkIndex,
        string textContent,
        float[] vector,
        string model,
        int pageNumber,
        string lang,
        TextChunkLink? sourceChunk) => new()
    {
        Id = Guid.NewGuid(),
        VectorDocumentId = vectorDocumentId,
        GameId = gameId,
        TextContent = textContent,
        Vector = new Vector(vector),
        Model = model,
        ChunkIndex = chunkIndex,
        PageNumber = pageNumber,
        Lang = lang,
        CreatedAt = DateTimeOffset.UtcNow,
        SourceChunkId = sourceChunk?.TextChunkId,
        RoleTags = sourceChunk?.RoleTags ?? 0,
    };
}

/// <summary>
/// #4384: il passaggio di <c>text_chunks</c> a cui appartiene una riga di <c>pgvector_embeddings</c>,
/// con il suo ruolo (il valore intero di <c>GameBookRole</c>).
/// </summary>
public sealed record TextChunkLink(Guid TextChunkId, int RoleTags)
{
    /// <summary>Il collegamento a un passaggio già costruito: il suo id e il suo ruolo.</summary>
    public static TextChunkLink From(TextChunkEntity chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        return new TextChunkLink(chunk.Id, (int)chunk.RoleTags);
    }
}
