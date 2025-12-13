using Api.Infrastructure.Entities.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.Administration;

/// <summary>
/// Entity configuration for AlertRuleEntity (Issue #921)
/// </summary>
public class AlertRuleEntityConfiguration : IEntityTypeConfiguration<AlertRuleEntity>
{
    public void Configure(EntityTypeBuilder<AlertRuleEntity> builder)
    {
        builder.ToTable("alert_rules");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.AlertType)
            .HasColumnName("alert_type")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Severity)
            .HasColumnName("severity")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(e => e.Threshold)
            .HasColumnName("threshold")
            .IsRequired();

        builder.Property(e => e.ThresholdUnit)
            .HasColumnName("threshold_unit")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.DurationMinutes)
            .HasColumnName("duration_minutes")
            .IsRequired();

        builder.Property(e => e.IsEnabled)
            .HasColumnName("is_enabled")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.UpdatedBy)
            .HasColumnName("updated_by")
            .IsRequired()
            .HasMaxLength(200);

        // Indexes
        builder.HasIndex(e => e.Name)
            .IsUnique()
            .HasDatabaseName("ix_alert_rules_name");

        builder.HasIndex(e => e.AlertType)
            .HasDatabaseName("ix_alert_rules_alert_type");

        builder.HasIndex(e => e.IsEnabled)
            .HasDatabaseName("ix_alert_rules_is_enabled");

        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("ix_alert_rules_created_at");
    }
}
