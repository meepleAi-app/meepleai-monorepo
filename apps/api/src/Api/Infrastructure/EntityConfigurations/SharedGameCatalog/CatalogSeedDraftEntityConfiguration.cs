using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SharedGameCatalog;

/// <summary>
/// Entity configuration for CatalogSeedDraftEntity (admin catalog seed workflow).
/// Issue #1903 — M1.3 EF Entity + Configuration + DbContext.
/// Spec: docs/superpowers/specs/2026-06-04-admin-catalog-seed-design.md §4.2
/// </summary>
internal sealed class CatalogSeedDraftEntityConfiguration : IEntityTypeConfiguration<CatalogSeedDraftEntity>
{
    public void Configure(EntityTypeBuilder<CatalogSeedDraftEntity> builder)
    {
        builder.ToTable("catalog_seed_drafts");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.BggId)
            .HasColumnName("bgg_id");

        builder.Property(e => e.WikidataQid)
            .HasColumnName("wikidata_qid")
            .HasMaxLength(32);

        builder.Property(e => e.SearchTermInput)
            .HasColumnName("search_term_input")
            .HasMaxLength(255);

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.ProvenanceJson)
            .HasColumnName("provenance_json")
            .HasColumnType("jsonb");

        builder.Property(e => e.RawPayloadJson)
            .HasColumnName("raw_payload_json")
            .HasColumnType("jsonb");

        builder.Property(e => e.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(500);

        builder.Property(e => e.ResultingSharedGameId)
            .HasColumnName("resulting_shared_game_id");

        builder.Property(e => e.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.FetchedAt)
            .HasColumnName("fetched_at");

        builder.Property(e => e.ApprovedAt)
            .HasColumnName("approved_at");

        builder.Property(e => e.ApprovedByUserId)
            .HasColumnName("approved_by_user_id");

        // #3651 lotto 5 — pattern xmin di ADR-060. `IsRowVersion()` su un `byte[]` non produce
        // il mapping alla colonna di sistema su Npgsql: serve la configurazione esplicita.
        builder.Property(e => e.Xmin)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Property(e => e.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("ix_catalog_seed_drafts_status");

        builder.HasIndex(e => e.BggId)
            .HasDatabaseName("ix_catalog_seed_drafts_bgg_id");

        builder.HasIndex(e => e.WikidataQid)
            .HasDatabaseName("ix_catalog_seed_drafts_wikidata_qid");

        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("ix_catalog_seed_drafts_created_at");

        // Soft-delete filter (CLAUDE.md pattern, like PdfDocumentEntity / SharedGameEntity)
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
