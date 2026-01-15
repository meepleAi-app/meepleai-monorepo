using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SharedGameCatalog;

internal class QuickQuestionEntityConfiguration : IEntityTypeConfiguration<QuickQuestionEntity>
{
    public void Configure(EntityTypeBuilder<QuickQuestionEntity> builder)
    {
        builder.ToTable("quick_questions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.SharedGameId).HasColumnName("shared_game_id").IsRequired();
        builder.Property(e => e.Text).HasColumnName("text").IsRequired().HasMaxLength(200);
        builder.Property(e => e.Emoji).HasColumnName("emoji").IsRequired().HasMaxLength(2);
        builder.Property(e => e.Category).HasColumnName("category").IsRequired().HasConversion<int>();
        builder.Property(e => e.DisplayOrder).HasColumnName("display_order").IsRequired().HasDefaultValue(0);
        builder.Property(e => e.IsGenerated).HasColumnName("is_generated").IsRequired().HasDefaultValue(false);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("NOW()");
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired().HasDefaultValue(true);

        // Indexes for common queries
        builder.HasIndex(e => e.SharedGameId).HasDatabaseName("ix_quick_questions_shared_game_id");
        builder.HasIndex(e => new { e.SharedGameId, e.DisplayOrder }).HasDatabaseName("ix_quick_questions_order");
        builder.HasIndex(e => new { e.SharedGameId, e.IsActive }).HasDatabaseName("ix_quick_questions_active");

        // One-to-many with SharedGame
        builder.HasOne(q => q.SharedGame)
            .WithMany(g => g.QuickQuestions)
            .HasForeignKey(q => q.SharedGameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
