using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

public class PdfDocumentEntityConfiguration : IEntityTypeConfiguration<PdfDocumentEntity>
{
    public void Configure(EntityTypeBuilder<PdfDocumentEntity> builder)
    {
        builder.ToTable("pdf_documents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.GameId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.FileName).IsRequired().HasMaxLength(256);
        builder.Property(e => e.FilePath).IsRequired().HasMaxLength(1024);
        builder.Property(e => e.FileSizeBytes).IsRequired();
        builder.Property(e => e.ContentType).IsRequired().HasMaxLength(128);
        builder.Property(e => e.UploadedByUserId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.UploadedAt).IsRequired();
        builder.Property(e => e.Metadata).HasMaxLength(2048);
        builder.Property(e => e.ProcessingStatus).IsRequired().HasMaxLength(32);
        builder.Property(e => e.ProcessingError).HasMaxLength(1024);
        builder.Property(e => e.ExtractedTables).HasMaxLength(8192);
        builder.Property(e => e.ExtractedDiagrams).HasMaxLength(8192);
        builder.Property(e => e.AtomicRules).HasMaxLength(8192);
        builder.HasOne(e => e.Game)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.UploadedBy)
            .WithMany()
            .HasForeignKey(e => e.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.GameId, e.UploadedAt });

        // AI-14: Hybrid search - PostgreSQL tsvector column managed by trigger
        // Ignore in EF Core since it's managed by database trigger, not application
        builder.Ignore(e => e.SearchVector);
    }
}
