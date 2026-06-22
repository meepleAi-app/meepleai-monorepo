using Api.BoundedContexts.SecurityAudit.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SecurityAudit;

/// <summary>
/// EF Core configuration for SecurityAudit AuditLogEntity.
/// Maps to <c>security_audit_logs</c> table to keep it distinct from the legacy
/// <c>audit_logs</c> table used by the Administration BC.
/// </summary>
internal class AuditLogEntityConfiguration : IEntityTypeConfiguration<AuditLogEntity>
{
    public void Configure(EntityTypeBuilder<AuditLogEntity> builder)
    {
        builder.ToTable("security_audit_logs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.ActorUserId);
        builder.Property(e => e.TargetUserId);
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(128);
        builder.Property(e => e.IpAddress).HasMaxLength(64);
        builder.Property(e => e.UserAgent).HasMaxLength(512);
        builder.Property(e => e.Timestamp).IsRequired();
        builder.Property(e => e.Metadata);
        builder.Property(e => e.CorrelationId).HasMaxLength(128);

        // Query-pattern indexes (audit dashboards filter by actor/target/type with timestamp ordering)
        builder.HasIndex(e => new { e.ActorUserId, e.Timestamp })
            .HasDatabaseName("IX_security_audit_logs_actor_user_id_timestamp");
        builder.HasIndex(e => new { e.TargetUserId, e.Timestamp })
            .HasDatabaseName("IX_security_audit_logs_target_user_id_timestamp");
        builder.HasIndex(e => new { e.EventType, e.Timestamp })
            .HasDatabaseName("IX_security_audit_logs_event_type_timestamp");
    }
}
