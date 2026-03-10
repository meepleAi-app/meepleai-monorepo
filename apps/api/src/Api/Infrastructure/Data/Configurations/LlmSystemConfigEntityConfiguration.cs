using Api.Infrastructure.Entities.SystemConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Data.Configurations;

/// <summary>
/// Issue #5498: EF Core configuration for LlmSystemConfig entity.
/// </summary>
public sealed class LlmSystemConfigEntityConfiguration : IEntityTypeConfiguration<LlmSystemConfigEntity>
{
    public void Configure(EntityTypeBuilder<LlmSystemConfigEntity> builder)
    {
        builder.ToTable("LlmSystemConfigs", "SystemConfiguration");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.CircuitBreakerFailureThreshold)
            .IsRequired()
            .HasDefaultValue(5);

        builder.Property(e => e.CircuitBreakerOpenDurationSeconds)
            .IsRequired()
            .HasDefaultValue(30);

        builder.Property(e => e.CircuitBreakerSuccessThreshold)
            .IsRequired()
            .HasDefaultValue(3);

        builder.Property(e => e.DailyBudgetUsd)
            .IsRequired()
            .HasPrecision(18, 6)
            .HasDefaultValue(10.00m);

        builder.Property(e => e.MonthlyBudgetUsd)
            .IsRequired()
            .HasPrecision(18, 6)
            .HasDefaultValue(100.00m);

        builder.Property(e => e.FallbackChainJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("[]");

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedAt)
            .IsRequired(false);

        builder.Property(e => e.UpdatedByUserId)
            .IsRequired(false);
    }
}
