using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Admin-curated draft entry from catalog seed import workflow.
/// One row per BGG ID / Wikidata Qid the admin enqueued. Lifecycle:
/// Pending → Fetched → Approved or Rejected (or FetchFailed).
/// Spec: docs/superpowers/specs/2026-06-04-admin-catalog-seed-design.md §4.2
/// Issue #1903.
/// </summary>
public class CatalogSeedDraftEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int? BggId { get; set; }
    public string? WikidataQid { get; set; }
    public string? SearchTermInput { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = "Pending"; // CatalogSeedStatus enum as string

    [Column(TypeName = "jsonb")]
    public string? ProvenanceJson { get; set; }

    [Column(TypeName = "jsonb")]
    public string? RawPayloadJson { get; set; }

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    public Guid? ResultingSharedGameId { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FetchedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }

    /// <summary>
    /// Concurrency token (#3651, ADR-060). Prima era <c>[Timestamp] byte[]? RowVersion</c> su una
    /// colonna <c>bytea</c>: Postgres non la popola, quindi il valore restava <c>null</c> e EF
    /// confrontava <c>NULL = NULL</c> a ogni update — protezione dichiarata e inesistente.
    /// Ora è la colonna di sistema <c>xmin</c>, gestita dal database: EF la legge, non la scrive.
    /// </summary>
    public uint Xmin { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
