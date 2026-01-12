using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SharedGameCatalog;

internal class GameDesignerEntityConfiguration : IEntityTypeConfiguration<GameDesignerEntity>
{
    public void Configure(EntityTypeBuilder<GameDesignerEntity> builder)
    {
        builder.ToTable("game_designers");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.Name).IsUnique().HasDatabaseName("ix_game_designers_name");

        // Many-to-many with SharedGames
        builder.HasMany(d => d.SharedGames)
            .WithMany(g => g.Designers)
            .UsingEntity<Dictionary<string, object>>(
                "shared_game_designers",
                j => j.HasOne<SharedGameEntity>().WithMany().HasForeignKey("shared_game_id").OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<GameDesignerEntity>().WithMany().HasForeignKey("game_designer_id").OnDelete(DeleteBehavior.Cascade),
                j => j.ToTable("shared_game_designers"));
    }
}
