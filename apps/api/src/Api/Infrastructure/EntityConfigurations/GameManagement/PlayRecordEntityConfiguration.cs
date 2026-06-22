using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameManagement;

/// <summary>
/// EF Core configuration for PlayRecordEntity.
/// Issue #3888: Play record tracking system.
/// </summary>
internal class PlayRecordEntityConfiguration : IEntityTypeConfiguration<PlayRecordEntity>
{
    public void Configure(EntityTypeBuilder<PlayRecordEntity> builder)
    {
        builder.ToTable("play_records");
        builder.HasKey(e => e.Id);

        // Game Association (Optional FK)
        builder.Property(e => e.GameId).IsRequired(false);
        builder.Property(e => e.GameName).IsRequired().HasMaxLength(255);
        builder.HasOne(e => e.Game)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.SetNull);

        // Ownership
        builder.Property(e => e.CreatedByUserId).IsRequired();
        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);  // Prevent user deletion with play history
        builder.Property(e => e.Visibility).IsRequired();
        builder.Property(e => e.GroupId).IsRequired(false);

        // Session Metadata
        builder.Property(e => e.SessionDate).IsRequired();
        builder.Property(e => e.StartTime).IsRequired(false);
        builder.Property(e => e.EndTime).IsRequired(false);
        builder.Property(e => e.Duration).IsRequired(false);
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(2000);
        builder.Property(e => e.Location).HasMaxLength(255);

        // Share token (#2437-2, GameNightPlaylist pattern)
        builder.Property(e => e.ShareToken).HasMaxLength(50);
        builder.Property(e => e.IsShared).HasDefaultValue(false).IsRequired();

        // Scoring Configuration (JSON)
        builder.Property(e => e.ScoringConfigJson)
            .IsRequired()
            .HasColumnType("jsonb");

        // Audit
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        // Soft Delete (issue #2439 — mirrors GameBook query-filter pattern)
        builder.Property(e => e.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();
        builder.Property(e => e.DeletedAt)
            .HasColumnName("deleted_at");

        // Issue #1938 / CF-2: source domain event id (nullable, UNIQUE partial).
        builder.Property(e => e.SourceEventId)
            .HasColumnName("source_event_id");

        // Indexes
        builder.HasIndex(e => e.GameId)
            .HasDatabaseName("IX_PlayRecords_GameId")
            .HasFilter("\"GameId\" IS NOT NULL");
        builder.HasIndex(e => e.CreatedByUserId)
            .HasDatabaseName("IX_PlayRecords_CreatedByUserId");
        builder.HasIndex(e => e.SessionDate)
            .HasDatabaseName("IX_PlayRecords_SessionDate")
            .IsDescending();
        builder.HasIndex(e => e.Status)
            .HasDatabaseName("IX_PlayRecords_Status");

        // Issue #1938 / CF-2: SourceEventId UNIQUE (partial — only when not null).
        // Guards against duplicate play records when an event handler is re-dispatched
        // (rolled-back outer tx in #1535, MediatR transient retry, hand-replay).
        builder.HasIndex(e => e.SourceEventId)
            .IsUnique()
            .HasDatabaseName("UX_play_records_source_event_id")
            .HasFilter("source_event_id IS NOT NULL");

        // Issue #2437-2: ShareToken UNIQUE (partial — only when not null).
        // Allows multiple non-shared records (all NULL) while enforcing token uniqueness.
        builder.HasIndex(e => e.ShareToken)
            .HasDatabaseName("IX_play_records_share_token")
            .IsUnique()
            .HasFilter("\"ShareToken\" IS NOT NULL");

        // Optimistic concurrency via PostgreSQL's xmin system column (ADR-060).
        // xmin is a Postgres system column — EF maps it but does NOT create the column.
        // ValueGeneratedOnAddOrUpdate tells EF the DB owns the value on every write.
        // IsConcurrencyToken() makes EF include it in UPDATE … WHERE xmin = @p0.
        // #2436 PR-B / #2437-1.
        builder.Property(e => e.Xmin)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Relationships
        builder.HasMany(e => e.Players)
            .WithOne(p => p.PlayRecord)
            .HasForeignKey(p => p.PlayRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        // Soft-delete query filter: deleted records are excluded from all queries
        // (history, statistics, get-by-id, can-view, can-edit). Issue #2439.
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
