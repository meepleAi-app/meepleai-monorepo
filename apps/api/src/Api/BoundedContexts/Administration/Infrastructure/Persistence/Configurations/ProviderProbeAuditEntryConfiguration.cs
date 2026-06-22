using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence.Configurations;

internal sealed class ProviderProbeAuditEntryConfiguration : IEntityTypeConfiguration<ProviderProbeAuditEntry>
{
    public void Configure(EntityTypeBuilder<ProviderProbeAuditEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("provider_probe_audit_entries", schema: "administration");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ProviderName).HasColumnName("provider_name").HasMaxLength(64).IsRequired();
        builder.Property(e => e.ActorId).HasColumnName("actor_id").IsRequired();
        builder.Property(e => e.TokenFingerprint).HasColumnName("token_fingerprint").HasMaxLength(8).IsRequired(false);
        builder.Property(e => e.Outcome).HasColumnName("outcome").HasConversion<int>().IsRequired();
        builder.Property(e => e.ErrorCode).HasColumnName("error_code").HasMaxLength(64).IsRequired(false);
        builder.Property(e => e.LatencyMs).HasColumnName("latency_ms").IsRequired();
        builder.Property(e => e.ProbedAt).HasColumnName("probed_at").IsRequired();

        builder.HasIndex(e => new { e.ProviderName, e.ProbedAt })
               .HasDatabaseName("ix_provider_probe_audit_provider_probed_at")
               .IsDescending(false, true);
        builder.HasIndex(e => new { e.ActorId, e.ProbedAt })
               .HasDatabaseName("ix_provider_probe_audit_actor_probed_at")
               .IsDescending(false, true);
    }
}
