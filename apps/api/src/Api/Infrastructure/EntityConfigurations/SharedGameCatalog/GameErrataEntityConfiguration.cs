using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SharedGameCatalog;

internal class GameErrataEntityConfiguration : IEntityTypeConfiguration<GameErrataEntity>
{
    public void Configure(EntityTypeBuilder<GameErrataEntity> builder)
    {
        builder.ToTable("game_errata");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.SharedGameId).HasColumnName("shared_game_id").IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").IsRequired().HasColumnType("text");
        builder.Property(e => e.PageReference).HasColumnName("page_reference").IsRequired().HasMaxLength(100);
        builder.Property(e => e.PublishedDate).HasColumnName("published_date").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.SharedGameId).HasDatabaseName("ix_game_errata_shared_game_id");
        builder.HasIndex(e => e.PublishedDate).HasDatabaseName("ix_game_errata_published_date").IsDescending();

        // One-to-many with SharedGame
        builder.HasOne(e => e.SharedGame)
            .WithMany(g => g.Erratas)
            .HasForeignKey(e => e.SharedGameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
