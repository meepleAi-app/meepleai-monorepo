using Api.Infrastructure.Entities;
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
        builder.Property(e => e.GameId).IsRequired(false).HasMaxLength(64);
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

        // Deprecated: Keep for backward compatibility
        builder.Property(e => e.ProcessingStatus).IsRequired().HasMaxLength(32);

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
        builder.Property(e => e.ExtractedTables).HasMaxLength(8192);
        builder.Property(e => e.ExtractedDiagrams).HasMaxLength(8192);
        builder.Property(e => e.AtomicRules).HasMaxLength(8192);
        builder.HasOne(e => e.Game)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.UploadedBy)
            .WithMany()
            .HasForeignKey(e => e.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.GameId, e.UploadedAt });

        // AI-14: Hybrid search - PostgreSQL tsvector column managed by trigger
        // Ignore in EF Core since it's managed by database trigger, not application
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

        // PDF deduplication: SHA-256 content hash
        builder.Property(e => e.ContentHash)
            .HasMaxLength(64)
            .HasColumnName("content_hash")
            .IsRequired(false);

        builder.HasIndex(e => new { e.ContentHash, e.GameId })
            .HasDatabaseName("ix_pdf_documents_content_hash_game_id");

        builder.HasIndex(e => new { e.ContentHash, e.PrivateGameId })
            .HasDatabaseName("ix_pdf_documents_content_hash_private_game_id");
    }
}
