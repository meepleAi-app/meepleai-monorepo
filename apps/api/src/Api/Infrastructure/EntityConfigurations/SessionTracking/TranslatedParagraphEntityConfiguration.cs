using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SessionTracking;

/// <summary>
/// EF Core configuration for TranslatedParagraph.
/// Uses direct domain entity mapping (no separate persistence entity).
/// Append-only immutable history record — no UpdateAt/UpdatedBy audit columns.
/// Phase A0.1 (2026-05-19): moved from BoundedContexts/SessionTracking/Infrastructure/Persistence/Configurations
/// to canonical Infrastructure/EntityConfigurations location to align with the rest of the codebase.
/// Iter 1.B — Libro Game Nanolith dogfood demo.
/// </summary>
internal class TranslatedParagraphEntityConfiguration : IEntityTypeConfiguration<TranslatedParagraph>
{
    public void Configure(EntityTypeBuilder<TranslatedParagraph> builder)
    {
        builder.ToTable("translated_paragraphs", schema: "session_tracking");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CampaignId).HasColumnName("campaign_id").IsRequired();
        builder.Property(e => e.GameBookId).HasColumnName("game_book_id").IsRequired();
        builder.Property(e => e.PhotoArtifactId).HasColumnName("photo_artifact_id").IsRequired();
        builder.Property(e => e.ParagraphNumber).HasColumnName("paragraph_number").IsRequired();
        builder.Property(e => e.PageType).HasColumnName("page_type").IsRequired();
        builder.Property(e => e.SourceTextEn).HasColumnName("source_text_en").HasColumnType("text").IsRequired();
        builder.Property(e => e.TranslatedTextIt).HasColumnName("translated_text_it").HasColumnType("text").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();

        // Issue #886: AppliedGlossaryTerms is mapped natively as a primitive collection.
        // Npgsql translates `string[]` ↔ Postgres `text[]` directly, no value converter needed.
        // The previous HasConversion(IReadOnlyList<string> → string[]) failed at model
        // finalization because EF Core 8+ auto-inserts an `IEnumerable<string> → string`
        // element converter that cannot be composed with the entity-level converter.
        builder.Property(e => e.AppliedGlossaryTerms)
            .HasColumnName("applied_glossary_terms")
            .HasColumnType("text[]")
            .IsRequired();

        // C3 (2026-05-19): per-book uniqueness — a paragraph number is unique within
        // (campaign, book), not globally per campaign, because a campaign can now
        // span multiple books (e.g. Press Start + Rules + Encounter).
        // Supersedes the previous non-unique (campaign_id, paragraph_number) index
        // which was dropped in the AddGameBookIdToTranslatedParagraph migration.
        builder.HasIndex(e => new { e.CampaignId, e.GameBookId, e.ParagraphNumber })
            .IsUnique()
            .HasDatabaseName("ux_translated_paragraphs_campaign_book_paragraph");
    }
}
