using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.KnowledgeBase;

/// <summary>
/// EF Core configuration for AgentGameStateSnapshotEntity.
/// Issue #3493: PostgreSQL Schema Extensions for Multi-Agent System.
/// </summary>
internal class AgentGameStateSnapshotEntityConfiguration : IEntityTypeConfiguration<AgentGameStateSnapshotEntity>
{
    public void Configure(EntityTypeBuilder<AgentGameStateSnapshotEntity> builder)
    {
        // Issue #3547: Renamed to avoid conflict with GameManagement's game_state_snapshots
        builder.ToTable("agent_game_state_snapshots");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.GameId)
            .IsRequired();

        builder.Property(e => e.AgentSessionId)
            .IsRequired();

        builder.Property(e => e.BoardStateJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(e => e.TurnNumber)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        // Vector embedding for position similarity search
        // TEMPORARY: Commented out until pgvector migration is created (Issue #3533)
        // builder.Property(e => e.Embedding)
        //     .HasColumnType("vector(1536)");

        // Indexes for query performance
        builder.HasIndex(e => e.GameId);
        builder.HasIndex(e => e.AgentSessionId);
        builder.HasIndex(e => new { e.GameId, e.TurnNumber });
        builder.HasIndex(e => e.CreatedAt);

        // Foreign key to AgentSession
        builder.HasOne(e => e.AgentSession)
            .WithMany()
            .HasForeignKey(e => e.AgentSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
