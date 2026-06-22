using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SharedGameCatalog;

/// <summary>
/// EF Core configuration for <see cref="SharedGameTranslationEntity"/>.
/// Backs the BE foundation of the shared-game translations feature
/// (issue #2339 — sub-PR 1/3).
/// </summary>
/// <remarks>
/// <para>
/// Indexing strategy: a single partial unique index
/// <c>uq_active_translation_per_locale</c> enforces "at most one active row
/// per (shared_game_id, locale)" via <c>WHERE NOT is_deleted</c>. Three
/// additional partial lookup indices cover the resolver, admin filters and
/// reporting paths without indexing soft-deleted rows.
/// </para>
/// <para>
/// Concurrency: <c>xmin</c> is wired as a Postgres <c>xid</c> column with
/// <c>ValueGeneratedOnAddOrUpdate</c> and <c>IsConcurrencyToken</c> per
/// ADR-060.
/// </para>
/// </remarks>
internal class SharedGameTranslationEntityConfiguration
    : IEntityTypeConfiguration<SharedGameTranslationEntity>
{
#pragma warning disable MA0051 // Method is too long — EF fluent configuration is necessarily verbose.
    public void Configure(EntityTypeBuilder<SharedGameTranslationEntity> builder)
#pragma warning restore MA0051
    {
        builder.ToTable("shared_game_translations");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id");

        builder.Property(t => t.SharedGameId)
            .HasColumnName("shared_game_id")
            .IsRequired();

        builder.Property(t => t.Locale)
            .HasColumnName("locale")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(t => t.Title)
            .HasColumnName("title")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(t => t.Source)
            .HasColumnName("source")
            .HasMaxLength(32)
            .HasDefaultValue("manual")
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(t => t.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(t => t.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Property(t => t.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(t => t.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(t => t.DeletedBy)
            .HasColumnName("deleted_by");

        // ADR-060 — Postgres xmin system column as concurrency token.
        builder.Property(t => t.Xmin)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Partial unique index — only active rows participate in the constraint,
        // so soft-deletes do not block recreation of a (game, locale) pair.
        builder.HasIndex(t => new { t.SharedGameId, t.Locale })
            .HasFilter("NOT is_deleted")
            .IsUnique()
            .HasDatabaseName("uq_active_translation_per_locale");

        // Resolver path (per-game lookup) — soft-deleted rows excluded.
        builder.HasIndex(t => t.SharedGameId)
            .HasFilter("NOT is_deleted")
            .HasDatabaseName("ix_translations_shared_game_id");

        // Admin filter by locale (e.g. "show all IT translations").
        builder.HasIndex(t => t.Locale)
            .HasFilter("NOT is_deleted")
            .HasDatabaseName("ix_translations_locale");

        // Reporting by source (auto-openrouter vs manual etc.).
        builder.HasIndex(t => t.Source)
            .HasFilter("NOT is_deleted")
            .HasDatabaseName("ix_translations_source");

        // Cascading FK back to the parent shared_games row. SharedGame
        // intentionally exposes no Translations navigation — translations are
        // a separate aggregate fetched through the resolver/repository.
        builder.HasOne(t => t.SharedGame)
            .WithMany()
            .HasForeignKey(t => t.SharedGameId)
            .OnDelete(DeleteBehavior.Cascade);

        // Hide soft-deleted rows by default. Admin write paths must opt out
        // via IgnoreQueryFilters() when they need to restore a deleted row.
        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
