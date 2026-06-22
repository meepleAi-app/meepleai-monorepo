using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.Infrastructure.Entities.SystemConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Data.Configurations;

/// <summary>
/// Issue #1089: EF Core configuration for the singleton IncidentBanner row.
/// </summary>
public sealed class IncidentBannerStateEntityConfiguration : IEntityTypeConfiguration<IncidentBannerStateEntity>
{
    public void Configure(EntityTypeBuilder<IncidentBannerStateEntity> builder)
    {
        builder.ToTable("IncidentBannerState", "SystemConfiguration");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.Message)
            .IsRequired()
            .HasMaxLength(IncidentBannerState.MaxMessageLength)
            .HasDefaultValue(string.Empty);

        builder.Property(e => e.Severity)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.StartsAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(e => e.EndsAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Seed singleton row (inactive default banner).
        builder.HasData(new IncidentBannerStateEntity
        {
            Id = IncidentBannerState.SingletonId,
            Message = string.Empty,
            Severity = 0,
            IsActive = false,
            StartsAt = null,
            EndsAt = null,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedBy = null
        });
    }
}
