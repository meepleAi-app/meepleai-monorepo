using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameManagement;

/// <summary>
/// EF Core configuration for GameStrategyEntity.
/// Issue #4903: Game strategies API endpoint.
/// </summary>
internal sealed class GameStrategyEntityConfiguration : IEntityTypeConfiguration<GameStrategyEntity>
{
    public void Configure(EntityTypeBuilder<GameStrategyEntity> builder)
    {
        builder.ToTable("game_strategies");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).IsRequired();

        builder.Property(e => e.SharedGameId)
            .IsRequired();

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Content)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(e => e.Author)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Upvotes)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.Tags)
            .IsRequired()
            .HasColumnType("text")
            .HasDefaultValue("[]");

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.HasIndex(e => e.SharedGameId)
            .HasDatabaseName("IX_GameStrategies_SharedGameId");

        builder.HasIndex(e => new { e.SharedGameId, e.Upvotes })
            .HasDatabaseName("IX_GameStrategies_SharedGameId_Upvotes");
    }
}
