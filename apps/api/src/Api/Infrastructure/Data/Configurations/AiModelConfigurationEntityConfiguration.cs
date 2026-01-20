using Api.Infrastructure.Entities.SystemConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Data.Configurations;

public sealed class AiModelConfigurationEntityConfiguration : IEntityTypeConfiguration<AiModelConfigurationEntity>
{
    public void Configure(EntityTypeBuilder<AiModelConfigurationEntity> builder)
    {
        builder.ToTable("AiModelConfigurations", "SystemConfiguration");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ModelId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Priority)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.IsPrimary)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedAt)
            .IsRequired(false);

        // Issue #2596: Tier routing configuration
        builder.Property(e => e.ApplicableTier)
            .HasColumnName("applicable_tier")
            .IsRequired(false);

        builder.Property(e => e.EnvironmentType)
            .HasColumnName("environment_type")
            .IsRequired()
            .HasDefaultValue(0); // Production

        builder.Property(e => e.IsDefaultForTier)
            .HasColumnName("is_default_for_tier")
            .IsRequired()
            .HasDefaultValue(false);

        // Issue #2596: Composite index for tier routing lookups
        builder.HasIndex(e => new { e.ApplicableTier, e.EnvironmentType, e.IsDefaultForTier })
            .HasDatabaseName("IX_AiModelConfigurations_TierRouting");

        // JSON Settings (JSONB) - Issue #2520
        builder.Property(e => e.SettingsJson)
            .HasColumnName("settings_json")
            .HasColumnType("jsonb")
            .IsRequired();

        // JSON Usage Stats (JSONB) - Issue #2520
        builder.Property(e => e.UsageJson)
            .HasColumnName("usage_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasIndex(e => e.ModelId)
            .IsUnique()
            .HasDatabaseName("IX_AiModelConfigurations_ModelId");

        builder.HasIndex(e => e.Priority)
            .HasDatabaseName("IX_AiModelConfigurations_Priority");

        builder.HasIndex(e => new { e.IsPrimary, e.IsActive })
            .HasDatabaseName("IX_AiModelConfigurations_IsPrimary_IsActive");

        // Issue #2520: JSONB columns for flexible configuration
        builder.Property(e => e.SettingsJson)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValue("{}");

        builder.Property(e => e.PricingJson)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValue("{}");

        builder.Property(e => e.UsageJson)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValue("{}");
    }
}
