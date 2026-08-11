using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

internal class PdfImageRegionEntityConfiguration : IEntityTypeConfiguration<PdfImageRegionEntity>
{
    public void Configure(EntityTypeBuilder<PdfImageRegionEntity> builder)
    {
        builder.ToTable("pdf_image_regions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PdfDocumentId).HasColumnName("pdf_document_id");
        builder.Property(e => e.PageNumber).HasColumnName("page_number");
        builder.Property(e => e.X).HasColumnName("x");
        builder.Property(e => e.Y).HasColumnName("y");
        builder.Property(e => e.Width).HasColumnName("width");
        builder.Property(e => e.Height).HasColumnName("height");
        builder.Property(e => e.ElementType).HasColumnName("element_type").HasMaxLength(64).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.HasOne(e => e.PdfDocument).WithMany()
            .HasForeignKey(e => e.PdfDocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.PdfDocumentId).HasDatabaseName("ix_pdf_image_regions_pdf_document_id");
    }
}
