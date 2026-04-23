using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// EF configuration for <see cref="MechanicStatusAuditEntity"/> (ADR-051 T6 audit trail).
/// Append-only; rows are written by the repository in the same transaction as the analysis update.
/// </summary>
internal sealed class MechanicStatusAuditEntityConfiguration : IEntityTypeConfiguration<MechanicStatusAuditEntity>
{
    public void Configure(EntityTypeBuilder<MechanicStatusAuditEntity> builder)
    {
        builder.ToTable("mechanic_status_audit", t =>
        {
            t.HasCheckConstraint(
                "ck_mechanic_status_audit_status_range",
                "from_status BETWEEN 0 AND 3 AND to_status BETWEEN 0 AND 3");

            t.HasCheckConstraint(
                "ck_mechanic_status_audit_distinct_states",
                "from_status <> to_status");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").IsRequired();
        builder.Property(a => a.AnalysisId).HasColumnName("analysis_id").IsRequired();
        builder.Property(a => a.FromStatus).HasColumnName("from_status").IsRequired();
        builder.Property(a => a.ToStatus).HasColumnName("to_status").IsRequired();
        builder.Property(a => a.ActorId).HasColumnName("actor_id").IsRequired();

        builder.Property(a => a.Note)
            .HasColumnName("note")
            .HasMaxLength(2000);

        builder.Property(a => a.OccurredAt).HasColumnName("occurred_at").IsRequired();

        builder.HasIndex(a => a.AnalysisId).HasDatabaseName("ix_mechanic_status_audit_analysis_id");
        builder.HasIndex(a => new { a.AnalysisId, a.OccurredAt })
            .HasDatabaseName("ix_mechanic_status_audit_analysis_time");

        builder.HasOne(a => a.Analysis)
            .WithMany()
            .HasForeignKey(a => a.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
