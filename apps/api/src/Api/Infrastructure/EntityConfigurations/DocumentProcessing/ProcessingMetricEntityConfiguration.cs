using Api.Infrastructure.Entities.DocumentProcessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.DocumentProcessing;

/// <summary>
/// EF Core configuration for ProcessingMetricEntity.
/// Issue #4212: Historical metrics table configuration.
/// </summary>
internal sealed class ProcessingMetricEntityConfiguration : IEntityTypeConfiguration<ProcessingMetricEntity>
{
    public void Configure(EntityTypeBuilder<ProcessingMetricEntity> builder)
    {
        builder.ToTable("pdf_processing_metrics");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Step)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.DurationSeconds)
            .IsRequired()
            .HasColumnType("numeric(10,2)");

        builder.Property(e => e.PdfSizeBytes)
            .IsRequired();

        builder.Property(e => e.PageCount)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Foreign key to pdf_documents
        builder.HasOne(e => e.PdfDocument)
            .WithMany()
            .HasForeignKey(e => e.PdfDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for fast historical queries
        builder.HasIndex(e => new { e.Step, e.CreatedAt })
            .HasDatabaseName("IX_pdf_processing_metrics_step_created_at")
            .IsDescending(false, true); // DESC on CreatedAt

        // Index for FK lookups
        builder.HasIndex(e => e.PdfDocumentId)
            .HasDatabaseName("IX_pdf_processing_metrics_pdf_document_id");
    }
}
