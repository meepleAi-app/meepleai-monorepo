using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// EF configuration for <see cref="CatalogSyncRunEntity"/> (#1861, F4-A6 BE).
/// Table <c>catalog_sync_runs</c>: tracks each catalog ingestion run (BGG cron / CSV import / manual).
/// </summary>
internal sealed class CatalogSyncRunEntityConfiguration : IEntityTypeConfiguration<CatalogSyncRunEntity>
{
    public void Configure(EntityTypeBuilder<CatalogSyncRunEntity> builder)
    {
        builder.ToTable("catalog_sync_runs", t =>
        {
            t.HasCheckConstraint(
                "ck_catalog_sync_runs_status_range",
                "status BETWEEN 0 AND 4");
            t.HasCheckConstraint(
                "ck_catalog_sync_runs_provider_range",
                "provider BETWEEN 0 AND 2");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id").IsRequired();

        builder.Property(r => r.Provider)
            .HasColumnName("provider")
            .HasColumnType("int")
            .IsRequired();

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasColumnType("int")
            .HasDefaultValue(CatalogSyncStatus.Queued)
            .IsRequired();

        builder.Property(r => r.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.TriggeredByUserId)
            .HasColumnName("triggered_by_user_id");

        builder.Property(r => r.ItemsAdded)
            .HasColumnName("items_added")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(r => r.ItemsUpdated)
            .HasColumnName("items_updated")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(r => r.ItemsFailed)
            .HasColumnName("items_failed")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(r => r.ErrorCode)
            .HasColumnName("error_code")
            .HasMaxLength(100);

        builder.Property(r => r.ErrorDetail)
            .HasColumnName("error_detail");

        builder.Property(r => r.LogTailJsonPath)
            .HasColumnName("log_tail_json_path")
            .HasMaxLength(500);

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.StartedAt)
            .HasColumnName("started_at");

        builder.Property(r => r.CompletedAt)
            .HasColumnName("completed_at");

        // === Indexes ===
        // Primary access pattern: SyncRunTimeline ordered by StartedAt DESC (admin UI history)
        builder.HasIndex(r => r.StartedAt)
            .HasDatabaseName("ix_catalog_sync_runs_started_at")
            .IsDescending();

        // Status filter for "currently running" check in TriggerCatalogSyncCommand 409 conflict
        builder.HasIndex(r => r.Status)
            .HasDatabaseName("ix_catalog_sync_runs_status")
            .HasFilter("status = 1"); // 1 = Running

        // === FK to users (nullable — cron-triggered runs have no user) ===
        builder.HasOne(r => r.TriggeredByUser)
            .WithMany()
            .HasForeignKey(r => r.TriggeredByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
