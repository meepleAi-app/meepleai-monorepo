using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

internal class AgentEntityConfiguration : IEntityTypeConfiguration<AgentEntity>
{
    public void Configure(EntityTypeBuilder<AgentEntity> builder)
    {
        builder.ToTable("agents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Type)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.StrategyName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.StrategyParametersJson)
            .IsRequired()
            .HasColumnType("jsonb"); // PostgreSQL JSONB for efficient querying

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.LastInvokedAt);

        builder.Property(e => e.InvocationCount)
            .IsRequired()
            .HasDefaultValue(0);

        // Issue #4682: Agent-Game association + User ownership
        builder.Property(e => e.GameId)
            .IsRequired(false);

        builder.Property(e => e.CreatedByUserId)
            .IsRequired(false);

        builder.HasOne(e => e.Game)
            .WithMany(g => g.Agents)
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes for common queries
        builder.HasIndex(e => e.Name).IsUnique();
        builder.HasIndex(e => e.Type);
        builder.HasIndex(e => e.IsActive);
        builder.HasIndex(e => e.LastInvokedAt);

        // Issue #4682: Indexes for agent-game queries
        builder.HasIndex(e => e.CreatedByUserId);
        builder.HasIndex(e => new { e.GameId, e.CreatedByUserId });
        builder.HasIndex(e => new { e.GameId, e.Type });
    }
}
