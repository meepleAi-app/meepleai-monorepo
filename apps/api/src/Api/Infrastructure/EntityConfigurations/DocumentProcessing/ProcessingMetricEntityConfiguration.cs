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

        // Issue #1938 / CF-2: source domain event id (nullable, UNIQUE partial).
        builder.Property(e => e.SourceEventId)
            .HasColumnName("source_event_id");

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

        // Issue #1938 / CF-2: SourceEventId UNIQUE (partial — only when not null).
        // Guards against duplicate metric rows when an event handler is re-dispatched
        // (rolled-back outer tx in #1535, MediatR transient retry, hand-replay).
        builder.HasIndex(e => e.SourceEventId)
            .IsUnique()
            .HasDatabaseName("UX_pdf_processing_metrics_source_event_id")
            .HasFilter("source_event_id IS NOT NULL");
    }
}
