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
    public string ImageUrl { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
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
    /// Optimistic concurrency token. Managed by the database.
    /// Spec-panel recommendation C-3.
    /// </summary>
    public byte[]? RowVersion { get; set; }

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
    /// Stored in R2 at <c>covers/pdf/{SharedGameId}/{key}-preview.webp</c>.
    /// Has higher priority than Wikidata (L2) and user-uploaded (L3) covers
    /// but only when a PDF with a valid cover has been uploaded and processed.
    /// Resolved to BlobCategory.GameImage when computing CoverUrl in DTOs.
    /// </summary>
    public string? PdfCoverR2Key { get; set; }

    /// <summary>Source URL on Wikimedia Commons; surfaced in the attribution footer.</summary>
    public string? WikidataCoverSourceUrl { get; set; }

    /// <summary>License identifier (e.g. "CC-BY-SA-4.0"); restricts the rendered attribution string.</summary>
    public string? WikidataCoverLicense { get; set; }

    /// <summary>Attribution string ready for display (author + license link).</summary>
    public string? WikidataCoverAttribution { get; set; }

    // Navigation properties (many-to-many)
    public ICollection<GameDesignerEntity> Designers { get; set; } = new List<GameDesignerEntity>();
    public ICollection<GamePublisherEntity> Publishers { get; set; } = new List<GamePublisherEntity>();
    public ICollection<GameCategoryEntity> Categories { get; set; } = new List<GameCategoryEntity>();
    public ICollection<GameMechanicEntity> Mechanics { get; set; } = new List<GameMechanicEntity>();

    // Navigation properties (one-to-many)
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
