using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.KnowledgeBase;

/// <summary>
/// EF Core configuration for StrategyPatternEntity.
/// Issue #3493: PostgreSQL Schema Extensions for Multi-Agent System.
/// </summary>
internal class StrategyPatternEntityConfiguration : IEntityTypeConfiguration<StrategyPatternEntity>
{
    public void Configure(EntityTypeBuilder<StrategyPatternEntity> builder)
    {
        builder.ToTable("strategy_patterns");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.GameId)
            .IsRequired();

        builder.Property(e => e.PatternName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.ApplicablePhase)
            .HasMaxLength(100);

        builder.Property(e => e.BoardConditionsJson)
            .HasColumnType("jsonb");

        builder.Property(e => e.MoveSequenceJson)
            .HasColumnType("jsonb");

        builder.Property(e => e.Source)
            .HasMaxLength(100);

        // Issue #3547: Vector embedding for pattern matching
        builder.Property(e => e.Embedding)
            .HasColumnType("vector(1024)");

        // Indexes for query performance
        builder.HasIndex(e => e.GameId);
        builder.HasIndex(e => new { e.GameId, e.ApplicablePhase });
        builder.HasIndex(e => e.EvaluationScore);
    }
}
