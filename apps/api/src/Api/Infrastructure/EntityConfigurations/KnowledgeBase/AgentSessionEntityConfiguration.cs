using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>
/// EF Core configuration for AgentSessionEntity.
/// Maps to "agent_sessions" table with FK constraints and cascade rules.
/// </summary>
internal class AgentSessionEntityConfiguration : IEntityTypeConfiguration<AgentSessionEntity>
{
    public void Configure(EntityTypeBuilder<AgentSessionEntity> builder)
    {
        builder.ToTable("agent_sessions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).IsRequired();

        // Foreign key to Agent
        builder.Property(e => e.AgentId).IsRequired();
        builder.HasOne(e => e.Agent)
            .WithMany()
            .HasForeignKey(e => e.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Foreign key to GameSession (GST dependency)
        builder.Property(e => e.GameSessionId).IsRequired();
        builder.HasOne(e => e.GameSession)
            .WithMany()
            .HasForeignKey(e => e.GameSessionId)
            .OnDelete(DeleteBehavior.Cascade); // Cascade delete when game_session is deleted

        // Foreign key to User
        builder.Property(e => e.UserId).IsRequired();
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Foreign key to Game
        builder.Property(e => e.GameId).IsRequired();
        builder.HasOne(e => e.Game)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.Restrict);

        // Foreign key to AgentTypology
        builder.Property(e => e.TypologyId).IsRequired();
        builder.HasOne(e => e.Typology)
            .WithMany()
            .HasForeignKey(e => e.TypologyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Game state stored as JSONB
        builder.Property(e => e.CurrentGameStateJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        // Session tracking
        builder.Property(e => e.StartedAt)
            .IsRequired();

        builder.Property(e => e.EndedAt)
            .IsRequired(false);

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Unique constraint: one agent session per user+game_session combination
        builder.HasIndex(e => new { e.GameSessionId, e.UserId })
            .IsUnique()
            .HasDatabaseName("IX_AgentSessions_GameSessionId_UserId_Unique");

        // Index on GameSessionId for FK performance
        builder.HasIndex(e => e.GameSessionId)
            .HasDatabaseName("IX_AgentSessions_GameSessionId");

        // Index on UserId for user-specific queries
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_AgentSessions_UserId");

        // Index on IsActive for filtering active sessions
        builder.HasIndex(e => e.IsActive)
            .HasDatabaseName("IX_AgentSessions_IsActive");

        // Composite index for active sessions by user
        builder.HasIndex(e => new { e.UserId, e.IsActive })
            .HasDatabaseName("IX_AgentSessions_UserId_IsActive");
    }
}
