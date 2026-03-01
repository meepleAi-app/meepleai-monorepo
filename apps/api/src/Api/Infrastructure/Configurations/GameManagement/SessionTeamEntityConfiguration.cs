using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.GameManagement;

/// <summary>
/// EF Core configuration for SessionTeamEntity.
/// Issue #4750: Table schema, indexes, and relationships for session teams.
/// </summary>
internal sealed class SessionTeamEntityConfiguration : IEntityTypeConfiguration<SessionTeamEntity>
{
    public void Configure(EntityTypeBuilder<SessionTeamEntity> builder)
    {
        builder.ToTable("session_teams");

        builder.HasKey(e => e.Id);

        // --- Scalar Properties ---

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.LiveGameSessionId)
            .HasColumnName("live_game_session_id")
            .IsRequired();

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Color)
            .HasColumnName("color")
            .HasMaxLength(7) // #RRGGBB
            .IsRequired();

        builder.Property(e => e.TeamScore)
            .HasColumnName("team_score")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.CurrentRank)
            .HasColumnName("current_rank")
            .IsRequired()
            .HasDefaultValue(0);

        // --- Indexes ---

        builder.HasIndex(e => new { e.LiveGameSessionId, e.Name })
            .HasDatabaseName("ix_session_teams_session_name")
            .IsUnique();

        builder.HasIndex(e => e.LiveGameSessionId)
            .HasDatabaseName("ix_session_teams_session_id");

        // --- Relationships ---
        // LiveGameSession → cascade configured in LiveGameSessionEntityConfiguration
        // Players → SetNull configured in SessionPlayerEntityConfiguration
    }
}
