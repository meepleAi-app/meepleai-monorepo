using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

internal class GameEntityConfiguration : IEntityTypeConfiguration<GameEntity>
{
    public void Configure(EntityTypeBuilder<GameEntity> builder)
    {
        builder.ToTable("games");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(128);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.HasIndex(e => e.Name).IsUnique();

        // Issue #2373 Phase 4: SharedGameCatalog FK relationship
        builder.Property(e => e.SharedGameId).IsRequired(false);
        builder.HasIndex(e => e.SharedGameId).HasDatabaseName("IX_Games_SharedGameId");
        builder.HasOne(e => e.SharedGame)
            .WithMany()
            .HasForeignKey(e => e.SharedGameId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}