using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Api.Models;

namespace Api.Infrastructure.Entities;

public class PdfDocumentEntity
{
    /// <summary>
    /// FilePath prefix that marks a demo/dogfood mock placeholder row (no real blob, no
    /// text_chunks), seeded by <c>SeedBadswormPersonaCommandHandler</c> as
    /// <c>seed/badsworm/&lt;game&gt;/rulebook.pdf</c>. Real content always lives under
    /// <c>pdfs/{id}/…</c>. Rows with this prefix are excluded from the RAG readiness signal
    /// (<c>SeedStateHealthCheck</c>) and from <c>StalePdfRecoveryService</c>, so their deliberate
    /// non-Ready demo states neither degrade <c>seed_state</c> nor get force-processed into
    /// <c>Failed</c> on the missing blob. Single source of truth for the marker (#3075).
    /// </summary>
    public const string DemoMockFilePathPrefix = "seed/";

    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = default!;
    public string FilePath { get; set; } = default!;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = "application/pdf";
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string? Metadata { get; set; } // JSON metadata

    // PDF-02: Text extraction fields
    public string? ExtractedText { get; set; }

    // Slice D: heading-aware re-index — persisted structured elements (JSON array
    // of ExtractedElement) captured at extraction time so a later re-index can
    // rebuild the heading-aware document without re-running the extractor.
    public string? StructuredElementsJson { get; set; }

    // Issue #4215: Granular 7-state tracking
    public string ProcessingState { get; set; } = "Pending"; // Enum stored as string: Pending, Uploading, Extracting, Chunking, Embedding, Indexing, Ready, Failed

    public DateTime? ProcessedAt { get; set; }
    public int? PageCount { get; set; }
    public int? CharacterCount { get; set; }
    public string? ProcessingError { get; set; }

    // Issue #4216: Retry mechanism tracking
    public int RetryCount { get; set; }
    public string? ErrorCategory { get; set; } // ErrorCategory enum: Network, Parsing, Quota, Service, Unknown
    public string? FailedAtState { get; set; } // PdfProcessingState where failure occurred

    // PDF-03: Structured data extraction fields
    public string? ExtractedTables { get; set; } // JSON array of tables
    public string? ExtractedDiagrams { get; set; } // JSON array of diagram metadata
    public string? AtomicRules { get; set; } // JSON array of atomic rules from tables
    public int? TableCount { get; set; }
    public int? DiagramCount { get; set; }
    public int? AtomicRuleCount { get; set; }

    // PDF-08: Progress tracking
    public string? ProcessingProgressJson { get; set; }

    // AI-09: Multi-language support
    public string Language { get; set; } = "en"; // ISO 639-1 code: en, it, de, fr, es

    // E5-1: Language confidence from detection and manual override
    public double? LanguageConfidence { get; set; }
    public string? LanguageOverride { get; set; }

    // AI-14: Hybrid search - PostgreSQL full-text search vector (automatically maintained by trigger)
    // This column is populated by the tsvector_update_pdf_documents trigger
    [Column("search_vector")]
    public string? SearchVector { get; set; }

    // Issue #2051: Multi-document collection support
    public Guid? CollectionId { get; set; }
    public string DocumentType { get; set; } = "base"; // base, expansion, errata, homerule
    public int SortOrder { get; set; }

    // Admin Wizard: Public library visibility (visible to all registered users)
    public bool IsPublic { get; set; }

    // Issue #2732: Shared game document support
    public Guid? SharedGameId { get; set; }
    public Guid? ContributorId { get; set; }
    public Guid? SourceDocumentId { get; set; }

    // Issue #3664: Private game PDF support
    public Guid? PrivateGameId { get; set; }

    // PDF deduplication: SHA-256 hash of file content
    public string? ContentHash { get; set; }

    // Issue #5443: Document classification for pipeline routing
    public string DocumentCategory { get; set; } = "Rulebook"; // DocumentCategory enum stored as string

    // Issue #5444: Self-referential FK for expansion/errata linkage to base rulebook
    public Guid? BaseDocumentId { get; set; }
    public PdfDocumentEntity? BaseDocument { get; set; }

    // Issue #5446: Copyright disclaimer and RAG active toggle
    public DateTime? CopyrightDisclaimerAcceptedAt { get; set; }
    public Guid? CopyrightDisclaimerAcceptedBy { get; set; }
    public bool IsActiveForRag { get; set; } = true;

    // RAG Copyright KB Cards: license tier for citation rendering (0=Copyrighted, 1=CreativeCommons, 2=PublicDomain)
    public int LicenseType { get; set; } // Default: 0 = Copyrighted

    // Issue #5447: User-editable version label
    public string? VersionLabel { get; set; }

    // Issue #1673: Pipeline indexer version applied at last reindex.
    // Nullable for backwards compat — backfilled to 'v0' on migration.
    public string? IndexerVersion { get; set; }

    // Issue #1802: Optimistic concurrency control via PostgreSQL xmin system column.
    // Auto-mapped to xmin by Npgsql when configured with .IsRowVersion(). Nullable
    // to avoid PhotoBatchUpload landmine (migration 20260524190307: NOT NULL caused
    // InsertCommand double-mapping bug under Npgsql).
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    // Issue #1687: User-editable display title (distinct from immutable FileName).
    public string? Title { get; set; }

    // Issue #1687: User-curated tags (deduped + lowercased + sorted on write).
    public List<string> Tags { get; set; } = new();

    // Issue #1687: Audit columns set by metadata-edit handlers (last-write-wins per D-3).
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    // Admin Wizard: Processing priority (Normal=0, Admin=10)
    public string ProcessingPriority { get; set; } = "Normal";

    // Issue #117: Batch upload grouping
    public Guid? BatchId { get; set; }

    // Issue #4219: Per-state timing tracking for metrics and ETA
    public DateTime? UploadingStartedAt { get; set; }
    public DateTime? ExtractingStartedAt { get; set; }
    public DateTime? ChunkingStartedAt { get; set; }
    public DateTime? EmbeddingStartedAt { get; set; }
    public DateTime? IndexingStartedAt { get; set; }

    [NotMapped]
    public ProcessingProgress? ProcessingProgress
    {
        get => ProcessingProgressJson == null
            ? null
            : JsonSerializer.Deserialize<ProcessingProgress>(ProcessingProgressJson);
        set => ProcessingProgressJson = value == null
            ? null
            : JsonSerializer.Serialize(value);
    }

    public UserEntity UploadedBy { get; set; } = default!;

    // Issue #2051: Navigation to collection
    public DocumentCollectionEntity? Collection { get; set; }

    // Issue #1831 (L4) — cover image rendered from the first significant
    // page of the PDF. Populated by ExtractPdfCoverImageStep during the
    // processing pipeline, stored in R2 at `covers/pdf/{Id}/{size}.webp`.
    // CoverR2Key is the prefix (without `-thumb` / `-preview` suffix); the
    // FE/server resolve the size at read time.
    public string? CoverR2Key { get; set; }

    // CoverGenerationStatus: enum stored as string — Pending | Generated |
    // Skipped (heuristic rejected first 3 pages as non-cover material) | Failed
    public string CoverGenerationStatus { get; set; } = "Pending";

    // Which 0-indexed page was selected as the cover. NULL when status != Generated.
    public int? CoverPageIndex { get; set; }

    // Last error string when CoverGenerationStatus = Failed; for diagnostics + retry.
    public string? CoverGenerationError { get; set; }
}
