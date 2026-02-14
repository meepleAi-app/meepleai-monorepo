using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.BusinessSimulations;

/// <summary>
/// EF Core configuration for ResourceForecast entity.
/// Issue #3726: Resource Forecasting Simulator (Epic #3688)
/// </summary>
internal class ResourceForecastEntityConfiguration : IEntityTypeConfiguration<ResourceForecast>
{
    public void Configure(EntityTypeBuilder<ResourceForecast> builder)
    {
        builder.ToTable("resource_forecasts");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.GrowthPattern)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.MonthlyGrowthRate)
            .IsRequired()
            .HasPrecision(5, 2);

        builder.Property(e => e.CurrentUsers)
            .IsRequired();

        builder.Property(e => e.CurrentDbSizeGb)
            .IsRequired()
            .HasPrecision(12, 4);

        builder.Property(e => e.CurrentDailyTokens)
            .IsRequired();

        builder.Property(e => e.CurrentCacheMb)
            .IsRequired()
            .HasPrecision(12, 4);

        builder.Property(e => e.CurrentVectorEntries)
            .IsRequired();

        builder.Property(e => e.DbPerUserMb)
            .IsRequired()
            .HasPrecision(10, 4);

        builder.Property(e => e.TokensPerUserPerDay)
            .IsRequired();

        builder.Property(e => e.CachePerUserMb)
            .IsRequired()
            .HasPrecision(10, 4);

        builder.Property(e => e.VectorsPerUser)
            .IsRequired();

        builder.Property(e => e.ProjectionsJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(e => e.RecommendationsJson)
            .HasColumnType("jsonb");

        builder.Property(e => e.ProjectedMonthlyCost)
            .IsRequired()
            .HasPrecision(18, 4);

        builder.Property(e => e.CreatedByUserId)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.HasIndex(e => e.CreatedByUserId)
            .HasDatabaseName("IX_ResourceForecasts_CreatedByUserId");

        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("IX_ResourceForecasts_CreatedAt")
            .IsDescending();
    }
}
