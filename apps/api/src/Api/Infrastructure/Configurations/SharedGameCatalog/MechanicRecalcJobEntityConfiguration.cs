using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// EF configuration for <see cref="MechanicRecalcJobEntity"/> (ADR-051 M2.1, Sprint 2 Task 6).
/// </summary>
/// <remarks>
/// Column names mirror the SQL DDL from migration <c>M2_1_MechanicRecalcJobs</c>. The
/// <c>triggered_by_user_id</c> FK references <c>users("Id")</c> ON DELETE RESTRICT. A partial
/// index on <c>(status, created_at) WHERE status IN (0,1)</c> was created by the migration and is
/// declared here so EF does not emit a phantom migration on <c>dotnet ef migrations add</c>.
///
/// EF Core auto-generates an FK index entry for <c>triggered_by_user_id</c> in the model snapshot
/// (named <c>IX_mechanic_recalc_jobs_triggered_by_user_id</c>). The corresponding
/// <c>CREATE INDEX</c> DDL is present in the Task 5 migration so the snapshot accurately reflects
/// the actual DB schema. No explicit <c>HasIndex</c> is needed here — EF infers it from the FK.
/// </remarks>
internal sealed class MechanicRecalcJobEntityConfiguration : IEntityTypeConfiguration<MechanicRecalcJobEntity>
{
    public void Configure(EntityTypeBuilder<MechanicRecalcJobEntity> builder)
    {
        builder.ToTable("mechanic_recalc_jobs", t =>
        {
            t.HasCheckConstraint(
                "ck_mechanic_recalc_jobs_status_range",
                "status BETWEEN 0 AND 4");
        });

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Id).HasColumnName("id").IsRequired();

        builder.Property(j => j.Status)
            .HasColumnName("status")
            .HasColumnType("int")
            .HasDefaultValue(RecalcJobStatus.Pending)
            .IsRequired();

        builder.Property(j => j.TriggeredByUserId)
            .HasColumnName("triggered_by_user_id")
            .IsRequired();

        builder.Property(j => j.Total)
            .HasColumnName("total")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(j => j.Processed)
            .HasColumnName("processed")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(j => j.Failed)
            .HasColumnName("failed")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(j => j.Skipped)
            .HasColumnName("skipped")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(j => j.ConsecutiveFailures)
            .HasColumnName("consecutive_failures")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(j => j.LastError)
            .HasColumnName("last_error");

        builder.Property(j => j.LastProcessedAnalysisId)
            .HasColumnName("last_processed_analysis_id");

        builder.Property(j => j.CancellationRequested)
            .HasColumnName("cancellation_requested")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(j => j.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(j => j.StartedAt)
            .HasColumnName("started_at");

        builder.Property(j => j.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(j => j.HeartbeatAt)
            .HasColumnName("heartbeat_at");

        // === Partial index to support FOR UPDATE SKIP LOCKED worker claim (Task 7) ===
        builder.HasIndex(j => new { j.Status, j.CreatedAt })
            .HasDatabaseName("ix_mechanic_recalc_jobs_status_created")
            .HasFilter("status IN (0, 1)");

        // === FK to users (ON DELETE RESTRICT — job row is an audit record) ===
        // EF auto-generates IX_mechanic_recalc_jobs_triggered_by_user_id in the model snapshot.
        // The matching CREATE INDEX DDL is present in M2_1_MechanicRecalcJobs so the snapshot
        // accurately reflects the actual DB schema. No explicit HasIndex call needed here.
        builder.HasOne(j => j.TriggeredByUser)
            .WithMany()
            .HasForeignKey(j => j.TriggeredByUserId)
            .OnDelete(DeleteBehavior.Restrict);

    }
}
