using Api.Infrastructure.Entities.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.Administration;

/// <summary>
/// Entity configuration for <see cref="HealthStatusAlertSentEntity"/>
/// (issue #1941 / iso-2 Fix 2). Single row per service name.
/// </summary>
internal class HealthStatusAlertSentEntityConfiguration
    : IEntityTypeConfiguration<HealthStatusAlertSentEntity>
{
    public void Configure(EntityTypeBuilder<HealthStatusAlertSentEntity> builder)
    {
        builder.ToTable("health_status_alerts_sent");

        builder.HasKey(e => e.ServiceName);

        builder.Property(e => e.ServiceName)
            .HasColumnName("service_name")
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(e => e.LastEventId)
            .HasColumnName("last_event_id")
            .IsRequired();

        builder.Property(e => e.LastSentAt)
            .HasColumnName("last_sent_at")
            .IsRequired();
    }
}
