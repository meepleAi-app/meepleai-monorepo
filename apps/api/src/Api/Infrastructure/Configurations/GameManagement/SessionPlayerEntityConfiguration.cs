using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.GameManagement;

/// <summary>
/// EF Core configuration for SessionPlayerEntity.
/// Issue #4750: Table schema, indexes, and relationships for session players.
/// </summary>
internal sealed class SessionPlayerEntityConfiguration : IEntityTypeConfiguration<SessionPlayerEntity>
{
    public void Configure(EntityTypeBuilder<SessionPlayerEntity> builder)
    {
        builder.ToTable("session_players");

        builder.HasKey(e => e.Id);

        // --- Scalar Properties ---

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.LiveGameSessionId)
            .HasColumnName("live_game_session_id")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id");

        builder.Property(e => e.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.AvatarUrl)
            .HasColumnName("avatar_url")
            .HasMaxLength(500);

        builder.Property(e => e.Color)
            .HasColumnName("color")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Role)
            .HasColumnName("role")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.TeamId)
            .HasColumnName("team_id");

        builder.Property(e => e.TotalScore)
            .HasColumnName("total_score")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.CurrentRank)
            .HasColumnName("current_rank")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.JoinedAt)
            .HasColumnName("joined_at")
            .IsRequired();

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        // --- Indexes ---

        builder.HasIndex(e => new { e.LiveGameSessionId, e.UserId })
            .HasDatabaseName("ix_session_players_session_user")
            .HasFilter("user_id IS NOT NULL")
            .IsUnique();

        builder.HasIndex(e => e.LiveGameSessionId)
            .HasDatabaseName("ix_session_players_session_id");

        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("ix_session_players_user_id")
            .HasFilter("user_id IS NOT NULL");

        // --- Relationships ---

        // LiveGameSession → cascade (session deletion removes all players)
        // Configured in LiveGameSessionEntityConfiguration

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Team)
            .WithMany(t => t.Players)
            .HasForeignKey(e => e.TeamId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
