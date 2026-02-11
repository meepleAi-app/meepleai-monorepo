using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.BusinessSimulations;

/// <summary>
/// EF Core configuration for CostScenario entity.
/// Issue #3725: Agent Cost Calculator (Epic #3688)
/// </summary>
internal class CostScenarioEntityConfiguration : IEntityTypeConfiguration<CostScenario>
{
    public void Configure(EntityTypeBuilder<CostScenario> builder)
    {
        builder.ToTable("cost_scenarios");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Strategy)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.ModelId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.MessagesPerDay)
            .IsRequired();

        builder.Property(e => e.ActiveUsers)
            .IsRequired();

        builder.Property(e => e.AvgTokensPerRequest)
            .IsRequired();

        builder.Property(e => e.CostPerRequest)
            .IsRequired()
            .HasPrecision(18, 8);

        builder.Property(e => e.DailyProjection)
            .IsRequired()
            .HasPrecision(18, 4);

        builder.Property(e => e.MonthlyProjection)
            .IsRequired()
            .HasPrecision(18, 4);

        builder.Property(e => e.Warnings)
            .HasColumnType("jsonb");

        builder.Property(e => e.CreatedByUserId)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.HasIndex(e => e.CreatedByUserId)
            .HasDatabaseName("IX_CostScenarios_CreatedByUserId");

        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("IX_CostScenarios_CreatedAt")
            .IsDescending();
    }
}
