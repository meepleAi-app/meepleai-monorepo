using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SessionTracking;

/// <summary>
/// EF Core configuration for GamebookGlossaryEntry.
/// Uses direct domain entity mapping (no separate persistence entity).
/// Phase A0.1 (2026-05-19): moved from BoundedContexts/SessionTracking/Infrastructure/Persistence/Configurations
/// to canonical Infrastructure/EntityConfigurations location to align with the rest of the codebase.
/// Iter 1.B — Libro Game Nanolith dogfood demo.
/// </summary>
internal class GamebookGlossaryEntryEntityConfiguration : IEntityTypeConfiguration<GamebookGlossaryEntry>
{
    public void Configure(EntityTypeBuilder<GamebookGlossaryEntry> builder)
    {
        builder.ToTable("gamebook_glossary_entries", schema: "session_tracking");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CampaignId).HasColumnName("campaign_id").IsRequired();
        builder.Property(e => e.TermEn).HasColumnName("term_en").HasMaxLength(200).IsRequired();
        builder.Property(e => e.TermIt).HasColumnName("term_it").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Source).HasColumnName("source").IsRequired();
        builder.Property(e => e.FirstSeenBookId).HasColumnName("first_seen_book_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => new { e.CampaignId, e.TermEn })
            .IsUnique()
            .HasDatabaseName("uq_gamebook_glossary_entries_campaign_term_en");
    }
}
