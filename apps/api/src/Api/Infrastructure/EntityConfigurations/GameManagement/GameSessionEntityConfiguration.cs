using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>
/// EF Core configuration for GameSessionEntity.
/// Maps to "GameSessions" table with optimized indexes for queries.
/// </summary>
internal class GameSessionEntityConfiguration : IEntityTypeConfiguration<GameSessionEntity>
{
    public void Configure(EntityTypeBuilder<GameSessionEntity> builder)
    {
        builder.ToTable("GameSessions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).IsRequired();

        // Foreign key to Game
        builder.Property(e => e.GameId).IsRequired();
        builder.HasOne(e => e.Game)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        // Session properties
        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(e => e.StartedAt)
            .IsRequired();

        builder.Property(e => e.CompletedAt)
            .IsRequired(false);

        builder.Property(e => e.WinnerName)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(e => e.Notes)
            .HasMaxLength(2000)
            .IsRequired(false);

        // Players stored as JSON
        builder.Property(e => e.PlayersJson)
            .IsRequired()
            .HasDefaultValue("[]");

        // Indexes for performance
        // Index on GameId for querying sessions by game
        builder.HasIndex(e => e.GameId)
            .HasDatabaseName("IX_GameSessions_GameId");

        // Composite index on Status + StartedAt for filtering active/recent sessions
        builder.HasIndex(e => new { e.Status, e.StartedAt })
            .HasDatabaseName("IX_GameSessions_Status_StartedAt");

        // Index on StartedAt for chronological queries
        builder.HasIndex(e => e.StartedAt)
            .HasDatabaseName("IX_GameSessions_StartedAt");
    }
}
