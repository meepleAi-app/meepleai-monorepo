using Api.Infrastructure.Entities.GameToolkit;
using Api.Infrastructure.Entities.UserLibrary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>
/// EF Core configuration for GameToolkit.
/// Issue #4972: Override flags + PrivateGameId FK support.
/// </summary>
internal class GameToolkitEntityConfiguration : IEntityTypeConfiguration<GameToolkitEntity>
{
    public void Configure(EntityTypeBuilder<GameToolkitEntity> builder)
    {
        builder.ToTable("GameToolkits");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).IsRequired();
        // GameId and PrivateGameId are mutually exclusive and both nullable.
        builder.Property(e => e.GameId).IsRequired(false);
        builder.Property(e => e.PrivateGameId).IsRequired(false);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Version).IsRequired().HasDefaultValue(1);
        builder.Property(e => e.CreatedByUserId).IsRequired();
        builder.Property(e => e.IsPublished).IsRequired().HasDefaultValue(false);

        // Override flags — Issue #4972
        builder.Property(e => e.OverridesTurnOrder).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.OverridesScoreboard).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.OverridesDiceSet).IsRequired().HasDefaultValue(false);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        // JSONB columns for tool configs
        builder.Property(e => e.DiceToolsJson).HasColumnType("jsonb");
        builder.Property(e => e.CardToolsJson).HasColumnType("jsonb");
        builder.Property(e => e.TimerToolsJson).HasColumnType("jsonb");
        builder.Property(e => e.CounterToolsJson).HasColumnType("jsonb");

        // JSONB columns for templates
        builder.Property(e => e.ScoringTemplateJson).HasColumnType("jsonb");
        builder.Property(e => e.TurnTemplateJson).HasColumnType("jsonb");
        builder.Property(e => e.StateTemplate).HasColumnType("jsonb");
        builder.Property(e => e.AgentConfig).HasColumnType("jsonb");

        // Concurrency token
        builder.Property(e => e.RowVersion).IsRowVersion();

        // FK to GameEntity (SharedGame) — optional
        builder.HasOne(e => e.Game)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to PrivateGameEntity — optional, mutually exclusive with GameId. Issue #4972.
        builder.HasOne(e => e.PrivateGame)
            .WithMany()
            .HasForeignKey(e => e.PrivateGameId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(e => e.GameId).HasDatabaseName("IX_GameToolkits_GameId");
        builder.HasIndex(e => e.PrivateGameId).HasDatabaseName("IX_GameToolkits_PrivateGameId");
        builder.HasIndex(e => e.IsPublished).HasDatabaseName("IX_GameToolkits_IsPublished");

        // Unique (GameId, Version) when GameId is set
        builder.HasIndex(e => new { e.GameId, e.Version })
            .IsUnique()
            .HasFilter("\"GameId\" IS NOT NULL")
            .HasDatabaseName("IX_GameToolkits_GameId_Version");

        // Unique (PrivateGameId, Version) when PrivateGameId is set. Issue #4972.
        builder.HasIndex(e => new { e.PrivateGameId, e.Version })
            .IsUnique()
            .HasFilter("\"PrivateGameId\" IS NOT NULL")
            .HasDatabaseName("IX_GameToolkits_PrivateGameId_Version");
    }
}
