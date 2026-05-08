using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for TranslatedParagraph.
/// Uses direct domain entity mapping (no separate persistence entity).
/// Iter 1.B — Libro Game Nanolith dogfood demo.
/// </summary>
internal sealed class TranslatedParagraphConfiguration : IEntityTypeConfiguration<TranslatedParagraph>
{
    public void Configure(EntityTypeBuilder<TranslatedParagraph> builder)
    {
        builder.ToTable("translated_paragraphs", schema: "session_tracking");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CampaignId).HasColumnName("campaign_id").IsRequired();
        builder.Property(e => e.PhotoArtifactId).HasColumnName("photo_artifact_id").IsRequired();
        builder.Property(e => e.ParagraphNumber).HasColumnName("paragraph_number").IsRequired();
        builder.Property(e => e.PageType).HasColumnName("page_type").IsRequired();
        builder.Property(e => e.SourceTextEn).HasColumnName("source_text_en").HasColumnType("text").IsRequired();
        builder.Property(e => e.TranslatedTextIt).HasColumnName("translated_text_it").HasColumnType("text").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();

        builder.Property(e => e.AppliedGlossaryTerms)
            .HasColumnName("applied_glossary_terms")
            .HasColumnType("text[]")
            .HasConversion(
                v => v.ToArray(),
                v => (IReadOnlyList<string>)v.ToList().AsReadOnly());

        builder.HasIndex(e => new { e.CampaignId, e.ParagraphNumber })
            .HasDatabaseName("ix_translated_paragraphs_campaign_paragraph");
    }
}
