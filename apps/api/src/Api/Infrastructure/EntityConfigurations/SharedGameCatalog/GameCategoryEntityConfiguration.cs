using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SharedGameCatalog;

internal class GameCategoryEntityConfiguration : IEntityTypeConfiguration<GameCategoryEntity>
{
    public void Configure(EntityTypeBuilder<GameCategoryEntity> builder)
    {
        builder.ToTable("game_categories");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
        builder.Property(e => e.Slug).HasColumnName("slug").IsRequired().HasMaxLength(100);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.Name).IsUnique().HasDatabaseName("ix_game_categories_name");
        builder.HasIndex(e => e.Slug).IsUnique().HasDatabaseName("ix_game_categories_slug");

        // Many-to-many with SharedGames
        builder.HasMany(c => c.SharedGames)
            .WithMany(g => g.Categories)
            .UsingEntity<Dictionary<string, object>>(
                "shared_game_categories",
                j => j.HasOne<SharedGameEntity>().WithMany().HasForeignKey("shared_game_id").OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<GameCategoryEntity>().WithMany().HasForeignKey("game_category_id").OnDelete(DeleteBehavior.Cascade),
                j => j.ToTable("shared_game_categories"));
    }
}
