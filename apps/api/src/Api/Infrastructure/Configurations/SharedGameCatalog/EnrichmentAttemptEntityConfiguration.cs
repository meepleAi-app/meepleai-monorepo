using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// EF configuration for <see cref="EnrichmentAttemptEntity"/> (#1874).
/// Table <c>enrichment_attempts</c>: BGG enrichment outcome history (success / failure).
/// </summary>
internal sealed class EnrichmentAttemptEntityConfiguration : IEntityTypeConfiguration<EnrichmentAttemptEntity>
{
    public void Configure(EntityTypeBuilder<EnrichmentAttemptEntity> builder)
    {
        builder.ToTable("enrichment_attempts");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.SharedGameId)
            .HasColumnName("shared_game_id")
            .IsRequired();

        builder.Property(e => e.CatalogSyncRunId)
            .HasColumnName("catalog_sync_run_id");

        builder.Property(e => e.AttemptedAt)
            .HasColumnName("attempted_at")
            .IsRequired();

        builder.Property(e => e.Success)
            .HasColumnName("success")
            .IsRequired();

        builder.Property(e => e.ErrorCode)
            .HasColumnName("error_code")
            .HasMaxLength(100);

        builder.Property(e => e.ErrorDetail)
            .HasColumnName("error_detail");

        builder.Property(e => e.RetryCount)
            .HasColumnName("retry_count")
            .HasDefaultValue(0)
            .IsRequired();

        // === Indexes ===
        // Primary access patterns:
        // 1. Failed items panel: WHERE success=false AND attempted_at >= cutoff GROUP BY shared_game_id
        builder.HasIndex(e => new { e.SharedGameId, e.AttemptedAt })
            .HasDatabaseName("ix_enrichment_attempts_shared_game_attempted_at")
            .IsDescending(false, true);

        builder.HasIndex(e => new { e.Success, e.AttemptedAt })
            .HasDatabaseName("ix_enrichment_attempts_success_attempted_at")
            .IsDescending(false, true);

        // === FKs ===
        builder.HasOne(e => e.SharedGame)
            .WithMany()
            .HasForeignKey(e => e.SharedGameId)
            .OnDelete(DeleteBehavior.Cascade);

        // CatalogSyncRun FK is nullable + Restrict so orphan history rows are preserved when a run is purged
        builder.HasOne(e => e.CatalogSyncRun)
            .WithMany()
            .HasForeignKey(e => e.CatalogSyncRunId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
