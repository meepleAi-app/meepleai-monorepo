using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SharedGameCatalog;

/// <summary>
/// Entity configuration for SharedGameEntity.
/// </summary>
internal class SharedGameEntityConfiguration : IEntityTypeConfiguration<SharedGameEntity>
{
#pragma warning disable MA0051 // Method is too long - EF Core fluent configuration is necessarily verbose
    public void Configure(EntityTypeBuilder<SharedGameEntity> builder)
#pragma warning restore MA0051
    {
        builder.ToTable("shared_games");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.BggId)
            .HasColumnName("bgg_id");

        builder.Property(e => e.AgentDefinitionId)
            .HasColumnName("agent_definition_id");

        builder.Property(e => e.Title)
            .HasColumnName("title")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.YearPublished)
            .HasColumnName("year_published")
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .IsRequired()
            .HasColumnType("text");

        builder.Property(e => e.MinPlayers)
            .HasColumnName("min_players")
            .IsRequired();

        builder.Property(e => e.MaxPlayers)
            .HasColumnName("max_players")
            .IsRequired();

        builder.Property(e => e.PlayingTimeMinutes)
            .HasColumnName("playing_time_minutes")
            .IsRequired();

        builder.Property(e => e.MinAge)
            .HasColumnName("min_age")
            .IsRequired();

        builder.Property(e => e.ComplexityRating)
            .HasColumnName("complexity_rating")
            .HasColumnType("decimal(3,2)");

        builder.Property(e => e.AverageRating)
            .HasColumnName("average_rating")
            .HasColumnType("decimal(4,2)");

        // Issue #2123 — BGG ToS compliance: image_url + thumbnail_url are now
        // nullable. The seeder writes NULL on every create path; covers are
        // resolved at runtime via CoverUrlResolver from R2 (PDF / Wikidata)
        // and the frontend renders a deterministic placeholder when null.
        builder.Property(e => e.ImageUrl)
            .HasColumnName("image_url")
            .HasMaxLength(1000);

        builder.Property(e => e.ThumbnailUrl)
            .HasColumnName("thumbnail_url")
            .HasMaxLength(1000);

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.RulesContent)
            .HasColumnName("rules_content")
            .HasColumnType("text");

        builder.Property(e => e.RulesLanguage)
            .HasColumnName("rules_language")
            .HasMaxLength(10);

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(e => e.ModifiedBy)
            .HasColumnName("modified_by");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(e => e.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.IsRagPublic)
            .HasColumnName("is_rag_public")
            .IsRequired()
            .HasDefaultValue(false);

        // S2 (library-to-game epic) — denormalized KB flag for catalog filter
        builder.Property(e => e.HasKnowledgeBase)
            .HasColumnName("has_knowledge_base")
            .IsRequired()
            .HasDefaultValue(false);

        // Optimistic concurrency (spec-panel C-3) — #3651: ora sulla colonna di sistema `xmin`.
        //
        // Era `.Property(e => e.RowVersion).HasColumnName("row_version").IsRowVersion()` su una
        // `bytea`: Postgres non la valorizza da sé, e il trigger `ef_update_row_version()` che lo
        // faceva è stato rimosso da #2305 quando le altre entità sono passate a xmin. Da allora il
        // token restava NULL su ogni riga e la protezione non scattava mai.
        //
        // Stesso pattern di LiveGameSessionEntityConfiguration:148-152 e GameNightPlaylist.
        builder.Property(e => e.Xmin)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Issue #1823 (umbrella #1821 L2) — Wikidata cover columns.
        builder.Property(e => e.WikidataCoverR2Key)
            .HasMaxLength(512)
            .HasColumnName("wikidata_cover_r2_key")
            .IsRequired(false);

        // Issue #1852 (umbrella #1821 L4) — PDF cover key denormalized from PdfDocumentEntity.
        builder.Property(e => e.PdfCoverR2Key)
            .HasMaxLength(512)
            .HasColumnName("pdf_cover_r2_key")
            .IsRequired(false);

        // Gap G2 (BGG cover re-upload) — BGG-sourced cover image re-uploaded to our storage.
        builder.Property(e => e.BggCoverR2Key)
            .HasMaxLength(256)
            .HasColumnName("bgg_cover_r2_key")
            .IsRequired(false);

        builder.Property(e => e.WikidataCoverSourceUrl)
            .HasMaxLength(2048)
            .HasColumnName("wikidata_cover_source_url")
            .IsRequired(false);

        builder.Property(e => e.WikidataCoverLicense)
            .HasMaxLength(64)
            .HasColumnName("wikidata_cover_license")
            .IsRequired(false);

        builder.Property(e => e.WikidataCoverAttribution)
            .HasMaxLength(512)
            .HasColumnName("wikidata_cover_attribution")
            .IsRequired(false);

        // Epic #3470 — admin manual-URL cover (fetched + re-hosted on R2).
        builder.Property(e => e.ManualCoverR2Key)
            .HasMaxLength(512)
            .HasColumnName("manual_cover_r2_key")
            .IsRequired(false);

        builder.Property(e => e.ManualCoverLicense)
            .HasMaxLength(64)
            .HasColumnName("manual_cover_license")
            .IsRequired(false);

        builder.Property(e => e.ManualCoverAttribution)
            .HasMaxLength(512)
            .HasColumnName("manual_cover_attribution")
            .IsRequired(false);

        builder.Property(e => e.ManualCoverSourceUrl)
            .HasMaxLength(2048)
            .HasColumnName("manual_cover_source_url")
            .IsRequired(false);

        builder.Property(e => e.ManualCoverAttestedBy)
            .HasColumnName("manual_cover_attested_by")
            .IsRequired(false);

        builder.Property(e => e.ManualCoverAttestedAt)
            .HasColumnName("manual_cover_attested_at")
            .IsRequired(false);

        // Issue #1823 Phase B M8 — Wikidata QID resolved against shared_games
        // before the cover-enrichment orchestrator runs (ADR DEC-3a). Max 32
        // chars covers Q-numbers well past the current Wikidata range.
        builder.Property(e => e.WikidataQid)
            .HasMaxLength(32)
            .HasColumnName("wikidata_qid")
            .IsRequired(false);

        // Issue #1823 Phase B M8 (ADR DEC-3i) — quarterly re-verification
        // timestamp. NULL until the M8 orchestrator first enriches successfully.
        builder.Property(e => e.WikidataQidLastVerifiedAt)
            .HasColumnName("wikidata_qid_last_verified_at")
            .IsRequired(false);

        // Global query filter for soft deletes
        builder.HasQueryFilter(e => !e.IsDeleted);

        // Relationships (Issue #4228)
        builder.HasOne<AgentDefinition>()
            .WithMany()
            .HasForeignKey(e => e.AgentDefinitionId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        // Issue #3236 — partial on is_deleted so a soft-deleted game does not permanently
        // reserve its BGG id (mirrors ix_private_games_owner_bgg). Without "AND is_deleted =
        // false", the unfiltered index blocks any new active game from reusing a bgg_id that a
        // soft-deleted (app-invisible) row still holds.
        builder.HasIndex(e => e.BggId)
            .IsUnique()
            .HasDatabaseName("ix_shared_games_bgg_id")
            .HasFilter("bgg_id IS NOT NULL AND is_deleted = false");

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("ix_shared_games_status")
            .HasFilter("is_deleted = false");

        builder.HasIndex(e => e.Title)
            .HasDatabaseName("ix_shared_games_title")
            .HasFilter("is_deleted = false");

        // S2 — partial index on the small "AI-ready" subset for the catalog filter
        builder.HasIndex(e => e.HasKnowledgeBase)
            .HasDatabaseName("ix_shared_games_has_knowledge_base")
            .HasFilter("has_knowledge_base = true");

        // Note: SearchVector index will be added manually in migration with tsvector type

        // Constraints
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_shared_games_year_published",
                "year_published = 0 OR (year_published > 1900 AND year_published <= 2100)");
            t.HasCheckConstraint("chk_shared_games_players",
                "(min_players = 0 AND max_players = 0) OR (min_players > 0 AND max_players >= min_players)");
            t.HasCheckConstraint("chk_shared_games_playing_time",
                "playing_time_minutes >= 0");
            t.HasCheckConstraint("chk_shared_games_min_age",
                "min_age >= 0");
            t.HasCheckConstraint("chk_shared_games_complexity",
                "complexity_rating IS NULL OR (complexity_rating >= 1.0 AND complexity_rating <= 5.0)");
            t.HasCheckConstraint("chk_shared_games_rating",
                "average_rating IS NULL OR (average_rating >= 1.0 AND average_rating <= 10.0)");
        });
    }
}
