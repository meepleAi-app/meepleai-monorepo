using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.KnowledgeBase;

/// <summary>
/// EF Core configuration for TierStrategyAccessEntity.
/// Issue #3438: Part of tier-strategy-model architecture.
/// </summary>
internal class TierStrategyAccessEntityConfiguration : IEntityTypeConfiguration<TierStrategyAccessEntity>
{
    public void Configure(EntityTypeBuilder<TierStrategyAccessEntity> builder)
    {
        builder.ToTable("tier_strategy_access");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Tier)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Strategy)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .IsRequired();

        // Unique constraint on Tier + Strategy combination
        builder.HasIndex(e => new { e.Tier, e.Strategy })
            .IsUnique();

        // Indexes for query performance
        builder.HasIndex(e => e.Tier);
        builder.HasIndex(e => e.Strategy);
        builder.HasIndex(e => e.IsEnabled);
    }
}
