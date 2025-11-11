using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

// AI-14: Hybrid search - text chunks table configuration
public class TextChunkEntityConfiguration : IEntityTypeConfiguration<TextChunkEntity>
{
    public void Configure(EntityTypeBuilder<TextChunkEntity> builder)
    {
        builder.ToTable("text_chunks");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.GameId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.PdfDocumentId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.Content).IsRequired(); // No max length for long text chunks
        builder.Property(e => e.ChunkIndex).IsRequired();
        builder.Property(e => e.PageNumber);
        builder.Property(e => e.CharacterCount).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        // SearchVector column is managed by PostgreSQL trigger, no need to configure here
        builder.HasOne(e => e.Game)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.PdfDocument)
            .WithMany()
            .HasForeignKey(e => e.PdfDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.GameId);
        builder.HasIndex(e => e.PdfDocumentId);
        builder.HasIndex(e => e.ChunkIndex);
        builder.HasIndex(e => e.PageNumber);
    }
}
