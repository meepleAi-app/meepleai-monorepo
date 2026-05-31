using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

internal class PdfDocumentEntityConfiguration : IEntityTypeConfiguration<PdfDocumentEntity>
{
    public void Configure(EntityTypeBuilder<PdfDocumentEntity> builder)
    {
        builder.ToTable("pdf_documents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.FileName).IsRequired().HasMaxLength(256);
        builder.Property(e => e.FilePath).IsRequired().HasMaxLength(1024);
        builder.Property(e => e.FileSizeBytes).IsRequired();
        builder.Property(e => e.ContentType).IsRequired().HasMaxLength(128);
        builder.Property(e => e.UploadedByUserId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.UploadedAt).IsRequired();
        builder.Property(e => e.Metadata).HasMaxLength(2048);
        // Issue #4215: New granular state tracking (enum stored as string)
        builder.Property(e => e.ProcessingState)
            .IsRequired()
            .HasMaxLength(32)
            .HasColumnName("processing_state")
            .HasDefaultValue("Pending")
            .HasConversion<string>(); // EF Core converts enum to string automatically

        builder.Property(e => e.ProcessingError).HasMaxLength(1024);

        // Issue #4216: Retry tracking configuration
        builder.Property(e => e.RetryCount)
            .IsRequired()
            .HasColumnName("retry_count")
            .HasDefaultValue(0);

        builder.Property(e => e.ErrorCategory)
            .HasMaxLength(32)
            .HasColumnName("error_category")
            .IsRequired(false);

        builder.Property(e => e.FailedAtState)
            .HasMaxLength(32)
            .HasColumnName("failed_at_state")
            .IsRequired(false);
        // E2E fix: structured content can be very large (1000+ rules), use TEXT instead of varchar(8192)
        builder.Property(e => e.ExtractedTables).HasColumnType("text");
        builder.Property(e => e.ExtractedDiagrams).HasColumnType("text");
        builder.Property(e => e.AtomicRules).HasColumnType("text");
        builder.Property(e => e.SharedGameId).IsRequired(false);

        builder.HasOne<SharedGameEntity>()
            .WithMany()
            .HasForeignKey(e => e.SharedGameId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.SharedGameId, e.UploadedAt })
            .HasDatabaseName("IX_pdf_documents_SharedGameId_UploadedAt");

        builder.HasOne(e => e.UploadedBy)
            .WithMany()
            .HasForeignKey(e => e.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // AI-14: Hybrid search - PostgreSQL GENERATED stored tsvector column
        // Computed from "ExtractedText" + "FileName" via migration AddSearchVectorColumns. Ignored by EF Core since it's DB-managed.
        builder.Ignore(e => e.SearchVector);

        // Issue #2051: Multi-document collection support
        builder.Property(e => e.CollectionId).HasMaxLength(64);
        builder.Property(e => e.DocumentType).IsRequired().HasMaxLength(50).HasDefaultValue("base");
        builder.Property(e => e.SortOrder).IsRequired().HasDefaultValue(0);

        builder.HasOne(e => e.Collection)
            .WithMany(c => c.PdfDocuments)
            .HasForeignKey(e => e.CollectionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.CollectionId);
        builder.HasIndex(e => new { e.CollectionId, e.SortOrder });

        // Issue #3664: Private game PDF support
        builder.Property(e => e.PrivateGameId).HasMaxLength(64).IsRequired(false);
        builder.HasIndex(e => e.PrivateGameId);

        // Admin Wizard: Processing priority for admin queue
        builder.Property(e => e.ProcessingPriority)
            .IsRequired()
            .HasMaxLength(16)
            .HasColumnName("processing_priority")
            .HasDefaultValue("Normal");

        // Issue #4219: Per-state timing fields for metrics and ETA
        builder.Property(e => e.UploadingStartedAt)
            .HasColumnName("uploading_started_at")
            .IsRequired(false);

        builder.Property(e => e.ExtractingStartedAt)
            .HasColumnName("extracting_started_at")
            .IsRequired(false);

        builder.Property(e => e.ChunkingStartedAt)
            .HasColumnName("chunking_started_at")
            .IsRequired(false);

        builder.Property(e => e.EmbeddingStartedAt)
            .HasColumnName("embedding_started_at")
            .IsRequired(false);

        builder.Property(e => e.IndexingStartedAt)
            .HasColumnName("indexing_started_at")
            .IsRequired(false);

        // Issue #5443: Document classification for pipeline routing
        builder.Property(e => e.DocumentCategory)
            .IsRequired()
            .HasMaxLength(32)
            .HasColumnName("document_category")
            .HasDefaultValue("Rulebook");

        builder.HasIndex(e => e.DocumentCategory)
            .HasDatabaseName("ix_pdf_documents_document_category");

        // Issue #5444: Self-referential FK for expansion/errata linkage
        builder.Property(e => e.BaseDocumentId)
            .HasColumnName("base_document_id")
            .IsRequired(false);

        builder.HasOne(e => e.BaseDocument)
            .WithMany()
            .HasForeignKey(e => e.BaseDocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.BaseDocumentId)
            .HasDatabaseName("ix_pdf_documents_base_document_id");

        // Issue #5446: Copyright disclaimer and RAG active toggle
        builder.Property(e => e.CopyrightDisclaimerAcceptedAt)
            .HasColumnName("copyright_disclaimer_accepted_at")
            .IsRequired(false);

        builder.Property(e => e.CopyrightDisclaimerAcceptedBy)
            .HasColumnName("copyright_disclaimer_accepted_by")
            .IsRequired(false);

        builder.Property(e => e.IsActiveForRag)
            .IsRequired()
            .HasColumnName("is_active_for_rag")
            .HasDefaultValue(true);

        builder.HasIndex(e => e.IsActiveForRag)
            .HasDatabaseName("ix_pdf_documents_is_active_for_rag");

        // RAG Copyright KB Cards: license tier for citation rendering
        builder.Property(e => e.LicenseType)
            .IsRequired()
            .HasColumnName("license_type")
            .HasDefaultValue(0);

        // Issue #5447: User-editable version label
        builder.Property(e => e.VersionLabel)
            .HasMaxLength(100)
            .HasColumnName("version_label")
            .IsRequired(false);

        // E5-1: Language confidence and override
        builder.Property(e => e.LanguageConfidence)
            .HasColumnName("language_confidence")
            .IsRequired(false);

        builder.Property(e => e.LanguageOverride)
            .HasMaxLength(10)
            .HasColumnName("language_override")
            .IsRequired(false);

        // PDF deduplication: SHA-256 content hash
        builder.Property(e => e.ContentHash)
            .HasMaxLength(64)
            .HasColumnName("content_hash")
            .IsRequired(false);

        builder.HasIndex(e => new { e.ContentHash, e.SharedGameId })
            .HasDatabaseName("ix_pdf_documents_content_hash_shared_game_id");

        builder.HasIndex(e => new { e.ContentHash, e.PrivateGameId })
            .HasDatabaseName("ix_pdf_documents_content_hash_private_game_id");

        // BE-1 #1588: composite index for cross-game per-user paginated listing
        // (GET /api/v1/kb-docs). Without this, the user-scoped query degrades to
        // a sequential scan on a multi-million-row table. The DESCENDING order on
        // ProcessedAt matches the sortBy=recent default (coalesced with UploadedAt
        // at query time; the index covers ProcessedAt only and Postgres handles
        // the COALESCE in the filter step).
        builder.HasIndex(e => new { e.UploadedByUserId, e.ProcessedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_pdf_documents_uploaded_by_user_id_processed_at_desc");

        // Issue #1687: User-editable display title (varchar(200) NULL).
        builder.Property(e => e.Title)
            .HasMaxLength(200)
            .HasColumnName("title")
            .IsRequired(false);

        // Issue #1687: Tags stored as PG text[] for native array operators
        // (@>, &&, ANY). Forward-compat with #1686 server-side facets.
        builder.Property(e => e.Tags)
            .HasColumnName("tags")
            .HasColumnType("text[]")
            .IsRequired()
            .HasDefaultValueSql("'{}'::text[]");

        // Issue #1687: Audit columns (last-write-wins per D-3, no RowVersion in v1).
        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired(false);

        builder.Property(e => e.UpdatedBy)
            .HasColumnName("updated_by")
            .IsRequired(false);

        // Issue #1687: GIN index on tags array for fast containment queries.
        // Forward-compat with #1686 facets (?tags=strategy filter) — cheap to add now.
        builder.HasIndex(e => e.Tags)
            .HasDatabaseName("IX_pdf_documents_tags_gin")
            .HasMethod("gin");
    }
}
