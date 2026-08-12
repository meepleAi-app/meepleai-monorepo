namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Entity for shared games in the catalog.
/// Persistence model for SharedGame aggregate root.
/// </summary>
public class SharedGameEntity
{
    public Guid Id { get; set; }
    public int? BggId { get; set; }
    public Guid? AgentDefinitionId { get; set; } // Issue #4228
    public string Title { get; set; } = string.Empty;
    public int YearPublished { get; set; }
    public string Description { get; set; } = string.Empty;
    public int MinPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public int PlayingTimeMinutes { get; set; }
    public int MinAge { get; set; }
    public decimal? ComplexityRating { get; set; }
    public decimal? AverageRating { get; set; }
    /// <summary>
    /// Issue #2123 — BGG ToS compliance: nullable. Covers are resolved at runtime by
    /// <see cref="Api.BoundedContexts.SharedGameCatalog.Application.Services.CoverUrlResolver"/>
    /// from R2-hosted assets (PDF, BGG-reuploaded, Wikidata). This column is kept for
    /// backward-compat with legacy admin tooling but seeders write <c>null</c> here.
    /// FE consumers MUST prefer <c>SharedGameDto.CoverUrl</c>.
    /// </summary>
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int Status { get; set; } // 0=Draft, 1=Published, 2=Archived
    public int GameDataStatus { get; set; } = 5; // Default Complete (5) for existing games
    public string? RulesContent { get; set; }
    public string? RulesLanguage { get; set; }
    public string? RulesExternalUrl { get; set; }
    public string? BggRawData { get; set; } // jsonb - raw BGG API response for repopulation
    public bool HasUploadedPdf { get; set; }
    // SearchVector managed by PostgreSQL trigger - not mapped by EF Core
    public Guid CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsRagPublic { get; set; }

    /// <summary>
    /// Denormalized flag set to true when at least one VectorDocument with this
    /// SharedGameId has been indexed. Maintained by
    /// VectorDocumentIndexedForKbFlagHandler (async event projection).
    /// Used by the public catalog filter "Solo giochi AI-ready" (S2 of library-to-game epic).
    /// </summary>
    public bool HasKnowledgeBase { get; set; }

    /// <summary>
    /// Token di concorrenza ottimistica, sulla colonna di sistema <c>xmin</c> di PostgreSQL.
    /// Spec-panel recommendation C-3; #3651 per la conversione.
    /// <para>
    /// Prima era <c>byte[]? RowVersion</c> su una colonna <c>bytea</c>. Postgres non popola una
    /// <c>bytea</c> da solo, e il trigger che lo faceva è stato rimosso da #2305 migrando le altre
    /// entità a <c>xmin</c>: da allora il token restava NULL, EF confrontava <c>NULL = NULL</c> a
    /// ogni update e due redattori concorrenti sulla stessa scheda si sovrascrivevano a vicenda in
    /// silenzio.
    /// </para>
    /// </summary>
    public uint Xmin { get; set; }

    /// <summary>
    /// Issue #1823 (umbrella #1821 L2) — Wikidata/Wikimedia Commons-sourced
    /// cover image for this catalog game. Stored in R2 at
    /// <c>covers/wikidata/{Id}/cover.webp</c>. Has lower priority than the
    /// user-uploaded cover (L3) and PDF-derived cover (L4) but supersedes
    /// the placeholder (L1). Nullable; populated by the Wikidata enrichment
    /// job — see issue #1823 for the SPARQL query + license validation rules
    /// (must be CC0 / CC-BY / CC-BY-SA + attribution stored alongside).
    /// </summary>
    public string? WikidataCoverR2Key { get; set; }

    /// <summary>
    /// Issue #1852 (umbrella #1821 L4) — PDF cover key denormalized from
    /// PdfDocumentEntity.CoverR2Key via PdfCoverGeneratedEventHandler.
    /// Stored in R2 at <c>covers/pdf/{pdfId:D}/cover-preview.webp</c> (Issue #2947:
    /// deterministic key keyed by the source PdfDocument's id, not SharedGameId).
    /// Has higher priority than Wikidata (L2) and user-uploaded (L3) covers
    /// but only when a PDF with a valid cover has been uploaded and processed.
    /// Resolved via <c>IBlobStorageService.GetPresignedUrlForRawKeyAsync</c>
    /// (raw-key primitive) when computing CoverUrl in DTOs — see CoverUrlResolver.
    /// </summary>
    public string? PdfCoverR2Key { get; set; }

    /// <summary>Source URL on Wikimedia Commons; surfaced in the attribution footer.</summary>
    public string? WikidataCoverSourceUrl { get; set; }

    /// <summary>License identifier (e.g. "CC-BY-SA-4.0"); restricts the rendered attribution string.</summary>
    public string? WikidataCoverLicense { get; set; }

    /// <summary>Attribution string ready for display (author + license link).</summary>
    public string? WikidataCoverAttribution { get; set; }

    /// <summary>
    /// Issue #1823 Phase B M8 — Wikidata QID (e.g. <c>"Q98056728"</c>) that identifies this
    /// game on Wikidata. Set by the M9 scheduler (or admin trigger) BEFORE the M8
    /// enrichment orchestrator runs; the orchestrator reads this column to resolve
    /// the SPARQL <c>wdt:P18</c> claim. Pattern <c>^Q\d+$</c>; max 32 chars covers
    /// Q-numbers well past the current Wikidata range (~Q120M as of 2026).
    /// </summary>
    public string? WikidataQid { get; set; }

    /// <summary>
    /// Issue #1823 Phase B M8 (ADR DEC-3i) — timestamp of the last successful
    /// QID + license re-verification. Used by the M15 quarterly re-verification
    /// cron to skip recently-verified games and by the M8 orchestrator to detect
    /// stale entries (&gt;90gg) that need refresh.
    /// </summary>
    public DateTime? WikidataQidLastVerifiedAt { get; set; }

    /// <summary>
    /// Gap G2 (issue: BGG cover re-upload).
    /// R2 key for cover image downloaded from BGG and re-uploaded to our storage.
    /// Resolved by CoverUrlResolver L2.5 layer (between L4 PDF and L2 Wikidata).
    /// Null when no BGG enrichment was applied or download failed.
    /// </summary>
    public string? BggCoverR2Key { get; set; }

    /// <summary>
    /// Epic #3470 — admin manual-URL cover source. The URL is fetched
    /// server-to-server, license-validated + re-encoded to WebP and re-hosted on
    /// R2 (never hotlinked); only this R2 key is persisted. Resolved as
    /// <c>CoverKind.Manual</c>. Null when no manual cover was set.
    /// </summary>
    public string? ManualCoverR2Key { get; set; }

    /// <summary>License identifier the admin attested for the manual cover (whitelist-validated).</summary>
    public string? ManualCoverLicense { get; set; }

    /// <summary>Attribution string for the manual cover, shown in the attribution footer.</summary>
    public string? ManualCoverAttribution { get; set; }

    /// <summary>Original source URL of the manual cover (audit/provenance; never rendered).</summary>
    public string? ManualCoverSourceUrl { get; set; }

    /// <summary>Admin who attested the manual cover's license.</summary>
    public Guid? ManualCoverAttestedBy { get; set; }

    /// <summary>When the manual cover's license was attested.</summary>
    public DateTime? ManualCoverAttestedAt { get; set; }

    /// <summary>
    /// Issue #1929 Task C Macro 3a (DEC-B-8, DEC-C-8) — E2E test seeding scope marker.
    /// Explicit column (NOT shadow property) to avoid EF Core 9 + Npgsql null-after-save bug.
    /// Stamped on insert by SeedTestLibraryGameCommandHandler; consumed by
    /// CleanupTestEntitiesCommandHandler.ExecuteDeleteAsync for cascade-delete scope.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.Column("test_run_id")]
    [System.ComponentModel.DataAnnotations.MaxLength(64)]
    public string? TestRunId { get; set; }

    // Navigation properties (many-to-many)
    public ICollection<GameDesignerEntity> Designers { get; set; } = new List<GameDesignerEntity>();
    public ICollection<GamePublisherEntity> Publishers { get; set; } = new List<GamePublisherEntity>();
    public ICollection<GameCategoryEntity> Categories { get; set; } = new List<GameCategoryEntity>();
    public ICollection<GameMechanicEntity> Mechanics { get; set; } = new List<GameMechanicEntity>();

    // Navigation properties (one-to-many)
    public ICollection<GameCoverAssignmentEntity> CoverAssignments { get; set; } = new List<GameCoverAssignmentEntity>();
    public ICollection<GameFaqEntity> Faqs { get; set; } = new List<GameFaqEntity>();
    public ICollection<GameErrataEntity> Erratas { get; set; } = new List<GameErrataEntity>();
    public ICollection<SharedGameDocumentEntity> Documents { get; set; } = new List<SharedGameDocumentEntity>();
    public ICollection<QuickQuestionEntity> QuickQuestions { get; set; } = new List<QuickQuestionEntity>();

    /// <summary>
    /// Contributors who have contributed to this game.
    /// Issue #2726: Application - Query per Dashboard Utente
    /// </summary>
    public ICollection<ContributorEntity> Contributors { get; set; } = new List<ContributorEntity>();
}
