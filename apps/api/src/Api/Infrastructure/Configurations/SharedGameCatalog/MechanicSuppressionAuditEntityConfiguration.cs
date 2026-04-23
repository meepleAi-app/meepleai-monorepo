using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// EF configuration for <see cref="MechanicSuppressionAuditEntity"/> (ADR-051 T5 + T6).
/// </summary>
internal sealed class MechanicSuppressionAuditEntityConfiguration : IEntityTypeConfiguration<MechanicSuppressionAuditEntity>
{
    public void Configure(EntityTypeBuilder<MechanicSuppressionAuditEntity> builder)
    {
        builder.ToTable("mechanic_suppression_audit", t =>
        {
            t.HasCheckConstraint(
                "ck_mechanic_suppression_audit_request_source_range",
                "request_source IS NULL OR request_source BETWEEN 0 AND 3");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").IsRequired();
        builder.Property(a => a.AnalysisId).HasColumnName("analysis_id").IsRequired();
        builder.Property(a => a.IsSuppressed).HasColumnName("is_suppressed").IsRequired();
        builder.Property(a => a.ActorId).HasColumnName("actor_id").IsRequired();

        builder.Property(a => a.Reason)
            .HasColumnName("reason")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(a => a.RequestSource).HasColumnName("request_source");
        builder.Property(a => a.RequestedAt).HasColumnName("requested_at");
        builder.Property(a => a.OccurredAt).HasColumnName("occurred_at").IsRequired();

        builder.HasIndex(a => a.AnalysisId).HasDatabaseName("ix_mechanic_suppression_audit_analysis_id");
        builder.HasIndex(a => new { a.AnalysisId, a.OccurredAt })
            .HasDatabaseName("ix_mechanic_suppression_audit_analysis_time");

        builder.HasOne(a => a.Analysis)
            .WithMany()
            .HasForeignKey(a => a.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
