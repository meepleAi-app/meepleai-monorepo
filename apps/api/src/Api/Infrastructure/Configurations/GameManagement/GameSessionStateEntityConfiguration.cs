using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.GameManagement;

/// <summary>
/// Entity Framework configuration for GameSessionState entity.
/// Issue #2403: GameSessionState Entity
/// </summary>
internal sealed class GameSessionStateEntityConfiguration : IEntityTypeConfiguration<GameSessionStateEntity>
{
    public void Configure(EntityTypeBuilder<GameSessionStateEntity> builder)
    {
        builder.ToTable("game_session_states");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(s => s.GameSessionId)
            .HasColumnName("game_session_id")
            .IsRequired();

        builder.Property(s => s.TemplateId)
            .HasColumnName("template_id")
            .IsRequired();

        builder.Property(s => s.CurrentStateJson)
            .HasColumnName("current_state_json")
            .HasColumnType("jsonb") // PostgreSQL JSONB for efficient JSON storage and querying
            .IsRequired();

        builder.Property(s => s.Version)
            .HasColumnName("version")
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(s => s.LastUpdatedAt)
            .HasColumnName("last_updated_at")
            .IsRequired();

        builder.Property(s => s.LastUpdatedBy)
            .HasColumnName("last_updated_by")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(s => s.RowVersion)
            .IsRowVersion();

        // Indexes for query performance
        builder.HasIndex(s => s.GameSessionId)
            .HasDatabaseName("ix_game_session_states_game_session_id")
            .IsUnique(); // One-to-one relationship with GameSession

        builder.HasIndex(s => s.TemplateId)
            .HasDatabaseName("ix_game_session_states_template_id");

        builder.HasIndex(s => s.LastUpdatedAt)
            .HasDatabaseName("ix_game_session_states_last_updated_at");

        // Foreign key to GameSession
        builder.HasOne(s => s.GameSession)
            .WithMany()
            .HasForeignKey(s => s.GameSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship with Snapshots
        builder.HasMany(s => s.Snapshots)
            .WithOne(snap => snap.SessionState)
            .HasForeignKey(snap => snap.SessionStateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
