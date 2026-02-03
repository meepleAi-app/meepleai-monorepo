using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.KnowledgeBase;

/// <summary>
/// EF Core configuration for StrategyModelMappingEntity.
/// Issue #3438: Part of tier-strategy-model architecture.
/// </summary>
internal class StrategyModelMappingEntityConfiguration : IEntityTypeConfiguration<StrategyModelMappingEntity>
{
    public void Configure(EntityTypeBuilder<StrategyModelMappingEntity> builder)
    {
        builder.ToTable("strategy_model_mapping");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Strategy)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.PrimaryModel)
            .IsRequired()
            .HasMaxLength(100);

        // FallbackModels stored as text array (PostgreSQL native array)
        builder.Property(e => e.FallbackModels)
            .HasColumnType("text[]");

        builder.Property(e => e.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.IsCustomizable)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.AdminOnly)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .IsRequired();

        // Unique constraint on Strategy
        builder.HasIndex(e => e.Strategy)
            .IsUnique();

        // Indexes for query performance
        builder.HasIndex(e => e.Provider);
        builder.HasIndex(e => e.AdminOnly);
    }
}
