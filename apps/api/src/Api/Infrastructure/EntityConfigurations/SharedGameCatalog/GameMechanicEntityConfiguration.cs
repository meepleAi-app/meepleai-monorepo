using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SharedGameCatalog;

internal class GameMechanicEntityConfiguration : IEntityTypeConfiguration<GameMechanicEntity>
{
    public void Configure(EntityTypeBuilder<GameMechanicEntity> builder)
    {
        builder.ToTable("game_mechanics");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
        builder.Property(e => e.Slug).HasColumnName("slug").IsRequired().HasMaxLength(100);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.Name).IsUnique().HasDatabaseName("ix_game_mechanics_name");
        builder.HasIndex(e => e.Slug).IsUnique().HasDatabaseName("ix_game_mechanics_slug");

        // Many-to-many with SharedGames
        builder.HasMany(m => m.SharedGames)
            .WithMany(g => g.Mechanics)
            .UsingEntity<Dictionary<string, object>>(
                "shared_game_mechanics",
                j => j.HasOne<SharedGameEntity>().WithMany().HasForeignKey("shared_game_id").OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<GameMechanicEntity>().WithMany().HasForeignKey("game_mechanic_id").OnDelete(DeleteBehavior.Cascade),
                j => j.ToTable("shared_game_mechanics"));
    }
}
