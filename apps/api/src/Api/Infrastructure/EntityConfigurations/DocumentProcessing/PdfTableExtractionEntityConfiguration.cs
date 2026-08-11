using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>EF configuration for <see cref="PdfTableExtractionEntity"/> (#3435 SP4).</summary>
internal sealed class PdfTableExtractionEntityConfiguration : IEntityTypeConfiguration<PdfTableExtractionEntity>
{
    public void Configure(EntityTypeBuilder<PdfTableExtractionEntity> builder)
    {
        builder.ToTable("pdf_table_extractions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.PdfDocumentId).HasColumnName("pdf_document_id").IsRequired();
        builder.Property(e => e.RegionHash).HasColumnName("region_hash").HasMaxLength(64).IsRequired();
        builder.Property(e => e.PageNumber).HasColumnName("page_number");
        builder.Property(e => e.X).HasColumnName("x");
        builder.Property(e => e.Y).HasColumnName("y");
        builder.Property(e => e.Width).HasColumnName("width");
        builder.Property(e => e.Height).HasColumnName("height");
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(e => e.Attempts).HasColumnName("attempts");
        builder.Property(e => e.TableMarkdown).HasColumnName("table_markdown");
        builder.Property(e => e.Confidence).HasColumnName("confidence");
        builder.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(32);
        builder.Property(e => e.TextChunkId).HasColumnName("text_chunk_id");
        builder.Property(e => e.LastError).HasColumnName("last_error");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(e => e.PdfDocument)
            .WithMany()
            .HasForeignKey(e => e.PdfDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Idempotency: one extraction record per (pdf, region). The region hash is deterministic
        // over the quantized bbox, so it is stable across the replace-by-pdf region re-seed.
        builder.HasIndex(e => new { e.PdfDocumentId, e.RegionHash })
            .IsUnique()
            .HasDatabaseName("ux_pdf_table_extractions_pdf_region");

        // Selector scans by status (+ attempts) to find pending / retry-eligible regions.
        builder.HasIndex(e => e.Status).HasDatabaseName("ix_pdf_table_extractions_status");
    }
}
