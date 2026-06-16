using Api.Infrastructure.Entities.GameToolkit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameToolkit;

/// <summary>
/// EF Core configuration for <see cref="AiToolkitSuggestionCacheEntity"/>.
/// UNIQUE index on <c>game_id</c> enforces the one-row-per-game invariant (ADR-069 #2383).
/// </summary>
internal sealed class AiToolkitSuggestionCacheEntityConfiguration
    : IEntityTypeConfiguration<AiToolkitSuggestionCacheEntity>
{
    public void Configure(EntityTypeBuilder<AiToolkitSuggestionCacheEntity> builder)
    {
        builder.ToTable("ai_toolkit_suggestion_cache");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.GameId).HasColumnName("game_id").IsRequired();
        builder.Property(e => e.SuggestionJson).HasColumnName("suggestion_json").IsRequired();
        builder.Property(e => e.GeneratedAt).HasColumnName("generated_at").IsRequired();
        builder.Property(e => e.KbVersion).HasColumnName("kb_version");

        builder.HasIndex(e => e.GameId)
            .HasDatabaseName("UX_ai_toolkit_suggestion_cache_game_id")
            .IsUnique();

        builder.HasIndex(e => e.GeneratedAt)
            .HasDatabaseName("IX_ai_toolkit_suggestion_cache_generated_at");
    }
}
