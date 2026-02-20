using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

internal class VectorDocumentEntityConfiguration : IEntityTypeConfiguration<VectorDocumentEntity>
{
    public void Configure(EntityTypeBuilder<VectorDocumentEntity> builder)
    {
        builder.ToTable("vector_documents", t =>
            t.HasCheckConstraint("CK_vector_documents_game_or_shared_game",
                "(game_id IS NOT NULL OR shared_game_id IS NOT NULL)"));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.GameId).IsRequired(false).HasColumnName("game_id");
        builder.Property(e => e.SharedGameId).IsRequired(false).HasColumnName("shared_game_id");
        builder.Property(e => e.PdfDocumentId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.ChunkCount).IsRequired();
        builder.Property(e => e.TotalCharacters).IsRequired();
        builder.Property(e => e.IndexingStatus).IsRequired().HasMaxLength(32);
        builder.Property(e => e.EmbeddingModel).IsRequired().HasMaxLength(128);
        builder.Property(e => e.EmbeddingDimensions).IsRequired();
        builder.HasOne(e => e.Game)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);
        builder.HasOne(e => e.SharedGame)
            .WithMany()
            .HasForeignKey(e => e.SharedGameId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
        builder.HasOne(e => e.PdfDocument)
            .WithMany()
            .HasForeignKey(e => e.PdfDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.GameId);
        builder.HasIndex(e => e.SharedGameId);
        builder.HasIndex(e => e.PdfDocumentId).IsUnique();
    }
}
