using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SharedGameCatalog;

internal class GamePublisherEntityConfiguration : IEntityTypeConfiguration<GamePublisherEntity>
{
    public void Configure(EntityTypeBuilder<GamePublisherEntity> builder)
    {
        builder.ToTable("game_publishers");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("NOW()");

        // Issue #3153: uniqueness on name is enforced by a CASE-INSENSITIVE functional
        // unique index `ix_game_publishers_name_lower` (lower(name)), created by the
        // AddLowerNameUniqueIndexToDesignersAndPublishers migration. EF cannot express a
        // functional expression index via HasIndex, so it is hand-written in raw SQL and
        // NOT model-tracked (the old case-sensitive `ix_game_publishers_name` is dropped
        // by that migration). Get-or-create lookups match on `lower(name)`.

        // Many-to-many with SharedGames
        builder.HasMany(p => p.SharedGames)
            .WithMany(g => g.Publishers)
            .UsingEntity<Dictionary<string, object>>(
                "shared_game_publishers",
                j => j.HasOne<SharedGameEntity>().WithMany().HasForeignKey("shared_game_id").OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<GamePublisherEntity>().WithMany().HasForeignKey("game_publisher_id").OnDelete(DeleteBehavior.Cascade),
                j => j.ToTable("shared_game_publishers"));
    }
}
