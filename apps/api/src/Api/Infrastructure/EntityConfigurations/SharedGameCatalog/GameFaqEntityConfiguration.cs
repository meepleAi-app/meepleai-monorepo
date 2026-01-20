using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SharedGameCatalog;

internal class GameFaqEntityConfiguration : IEntityTypeConfiguration<GameFaqEntity>
{
    public void Configure(EntityTypeBuilder<GameFaqEntity> builder)
    {
        builder.ToTable("game_faqs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.SharedGameId).HasColumnName("shared_game_id").IsRequired();
        builder.Property(e => e.Question).HasColumnName("question").IsRequired().HasMaxLength(500);
        builder.Property(e => e.Answer).HasColumnName("answer").IsRequired().HasColumnType("text");
        builder.Property(e => e.Order).HasColumnName("order").IsRequired().HasDefaultValue(0);
        builder.Property(e => e.UpvoteCount).HasColumnName("upvote_count").IsRequired().HasDefaultValue(0);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("NOW()");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => e.SharedGameId).HasDatabaseName("ix_game_faqs_shared_game_id");
        builder.HasIndex(e => new { e.SharedGameId, e.Order }).HasDatabaseName("ix_game_faqs_order");

        // One-to-many with SharedGame
        builder.HasOne(f => f.SharedGame)
            .WithMany(g => g.Faqs)
            .HasForeignKey(f => f.SharedGameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
