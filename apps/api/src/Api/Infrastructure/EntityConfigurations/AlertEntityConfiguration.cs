using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

// OPS-07: Alerting system
public class AlertEntityConfiguration : IEntityTypeConfiguration<AlertEntity>
{
    public void Configure(EntityTypeBuilder<AlertEntity> builder)
    {
        builder.ToTable("alerts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.AlertType).HasColumnName("alert_type").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Severity).HasColumnName("severity").HasMaxLength(20).IsRequired();
        builder.Property(e => e.Message).HasColumnName("message").IsRequired();
        builder.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
        builder.Property(e => e.TriggeredAt).HasColumnName("triggered_at").IsRequired();
        builder.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(e => e.ChannelSent).HasColumnName("channel_sent").HasColumnType("jsonb");

        // Index for querying active alerts
        builder.HasIndex(e => e.IsActive)
            .HasFilter("is_active = true");
        // Index for alert type and time-based queries
        builder.HasIndex(e => new { e.AlertType, e.TriggeredAt });
    }
}
