using Api.Infrastructure.Entities.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.Administration;

/// <summary>
/// Entity configuration for AlertConfigurationEntity (Issue #921)
/// </summary>
internal class AlertConfigurationEntityConfiguration : IEntityTypeConfiguration<AlertConfigurationEntity>
{
    public void Configure(EntityTypeBuilder<AlertConfigurationEntity> builder)
    {
        builder.ToTable("alert_configurations");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ConfigKey)
            .HasColumnName("config_key")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.ConfigValue)
            .HasColumnName("config_value")
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(e => e.Category)
            .HasColumnName("category")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.IsEncrypted)
            .HasColumnName("is_encrypted")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedBy)
            .HasColumnName("updated_by")
            .IsRequired()
            .HasMaxLength(200);

        // Indexes
        builder.HasIndex(e => e.ConfigKey)
            .IsUnique()
            .HasDatabaseName("ix_alert_configurations_config_key");

        builder.HasIndex(e => e.Category)
            .HasDatabaseName("ix_alert_configurations_category");

        builder.HasIndex(e => e.UpdatedAt)
            .HasDatabaseName("ix_alert_configurations_updated_at");
    }
}
