using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SharedGameCatalog;

/// <summary>
/// EF Core configuration for SharedGameDocumentEntity.
/// </summary>
internal class SharedGameDocumentEntityConfiguration : IEntityTypeConfiguration<SharedGameDocumentEntity>
{
    public void Configure(EntityTypeBuilder<SharedGameDocumentEntity> builder)
    {
        builder.ToTable("shared_game_documents");

        // Primary key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        // Foreign keys
        builder.Property(e => e.SharedGameId)
            .HasColumnName("shared_game_id")
            .IsRequired();

        builder.Property(e => e.PdfDocumentId)
            .HasColumnName("pdf_document_id")
            .IsRequired();

        // Document type (stored as int)
        builder.Property(e => e.DocumentType)
            .HasColumnName("document_type")
            .IsRequired();

        // Version string
        builder.Property(e => e.Version)
            .HasColumnName("version")
            .IsRequired()
            .HasMaxLength(20);

        // Active flag
        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(false);

        // Tags JSON (nullable, only for Homerule)
        builder.Property(e => e.TagsJson)
            .HasColumnName("tags_json")
            .HasColumnType("jsonb");

        // Audit fields
        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        // Indexes
        builder.HasIndex(e => e.SharedGameId)
            .HasDatabaseName("ix_shared_game_documents_shared_game_id");

        builder.HasIndex(e => e.PdfDocumentId)
            .HasDatabaseName("ix_shared_game_documents_pdf_document_id");

        // Index for finding active version by game and type
        builder.HasIndex(e => new { e.SharedGameId, e.DocumentType, e.IsActive })
            .HasDatabaseName("ix_shared_game_documents_active_version");

        // NOTE: Unique partial index for concurrency protection is created via raw SQL migration
        // (20260116100000_AddSingleActiveDocumentConstraint.cs)
        // CREATE UNIQUE INDEX ix_shared_game_documents_single_active
        // ON shared_game_documents (shared_game_id, document_type) WHERE is_active = true;
        // This guarantees at most ONE active document per (game, type) at database level

        // Unique constraint: one version per game + type + version string
        builder.HasIndex(e => new { e.SharedGameId, e.DocumentType, e.Version })
            .HasDatabaseName("ix_shared_game_documents_version_unique")
            .IsUnique();

        // Relationships
        builder.HasOne(d => d.SharedGame)
            .WithMany(g => g.Documents)
            .HasForeignKey(d => d.SharedGameId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.PdfDocument)
            .WithMany()
            .HasForeignKey(d => d.PdfDocumentId)
            .OnDelete(DeleteBehavior.Restrict); // Don't cascade delete PDFs
    }
}
