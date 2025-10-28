using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

public class VectorDocumentEntityConfiguration : IEntityTypeConfiguration<VectorDocumentEntity>
{
    public void Configure(EntityTypeBuilder<VectorDocumentEntity> builder)
    {
        builder.ToTable("vector_documents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.GameId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.PdfDocumentId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.ChunkCount).IsRequired();
        builder.Property(e => e.TotalCharacters).IsRequired();
        builder.Property(e => e.IndexingStatus).IsRequired().HasMaxLength(32);
        builder.Property(e => e.EmbeddingModel).IsRequired().HasMaxLength(128);
        builder.Property(e => e.EmbeddingDimensions).IsRequired();
        builder.HasOne(e => e.Game)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.PdfDocument)
            .WithMany()
            .HasForeignKey(e => e.PdfDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.GameId);
        builder.HasIndex(e => e.PdfDocumentId).IsUnique();
    }
}
