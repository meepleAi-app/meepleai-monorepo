using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.UserLibrary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

internal class VectorDocumentEntityConfiguration : IEntityTypeConfiguration<VectorDocumentEntity>
{
    public void Configure(EntityTypeBuilder<VectorDocumentEntity> builder)
    {
        builder.ToTable("vector_documents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.GameId).IsRequired(false).HasMaxLength(64);
        builder.Property(e => e.PdfDocumentId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.ChunkCount).IsRequired();
        builder.Property(e => e.TotalCharacters).IsRequired();
        builder.Property(e => e.IndexingStatus).IsRequired().HasMaxLength(32);
        builder.Property(e => e.EmbeddingModel).IsRequired().HasMaxLength(128);
        builder.Property(e => e.EmbeddingDimensions).IsRequired();
        builder.HasOne(e => e.Game)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.PdfDocument)
            .WithMany()
            .HasForeignKey(e => e.PdfDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        // Issue #4921: SharedGameId for admin KB cards (nullable cross-BC reference)
        builder.Property(e => e.SharedGameId).HasColumnName("shared_game_id");
        builder.HasIndex(e => e.SharedGameId);

        // Issue #4244: il gioco privato sta in una colonna sua, con la sua chiave esterna. CASCADE
        // vale per le cancellazioni fisiche; `PrivateGame.Delete()` è un soft delete e non la fa
        // scattare: lì la rimozione è esplicita.
        builder.Property(e => e.PrivateGameId).IsRequired(false);
        builder.HasOne<PrivateGameEntity>()
            .WithMany()
            .HasForeignKey(e => e.PrivateGameId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.PrivateGameId);

        builder.HasIndex(e => e.GameId);
        builder.HasIndex(e => e.PdfDocumentId).IsUnique();
    }
}
