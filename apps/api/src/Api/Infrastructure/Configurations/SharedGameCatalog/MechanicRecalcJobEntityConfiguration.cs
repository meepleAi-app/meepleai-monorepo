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
/// There is intentionally no <c>HasIndex</c> on <c>triggered_by_user_id</c> alone — the migration
/// DDL did not create one and adding it here would cause EF to emit a spurious migration.
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
        // Note: We explicitly suppress the EF-auto-generated FK index on triggered_by_user_id
        // because the DDL in M2_1_MechanicRecalcJobs did not create one; adding it here would
        // cause EF to emit a phantom migration.
        builder.HasOne(j => j.TriggeredByUser)
            .WithMany()
            .HasForeignKey(j => j.TriggeredByUserId)
            .OnDelete(DeleteBehavior.Restrict);

    }
}
