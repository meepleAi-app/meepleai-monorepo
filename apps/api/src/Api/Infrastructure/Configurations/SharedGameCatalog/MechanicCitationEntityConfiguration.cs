using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// EF configuration for <see cref="MechanicCitationEntity"/> (ADR-051 T1: quote ≤25 words).
/// </summary>
/// <remarks>
/// The 25-word CHECK constraint is enforced at DB level via whitespace tokenisation, mirroring
/// the domain factory's <c>MechanicCitation.CountWords</c>. Belt-and-braces defense: reject
/// non-compliant rows even if a future code path bypasses the domain factory.
/// </remarks>
internal sealed class MechanicCitationEntityConfiguration : IEntityTypeConfiguration<MechanicCitationEntity>
{
    public void Configure(EntityTypeBuilder<MechanicCitationEntity> builder)
    {
        builder.ToTable("mechanic_citations", t =>
        {
            t.HasCheckConstraint(
                "ck_mechanic_citations_page_positive",
                "pdf_page > 0");

            t.HasCheckConstraint(
                "ck_mechanic_citations_quote_not_empty",
                "char_length(btrim(quote)) > 0");

            t.HasCheckConstraint(
                "ck_mechanic_citations_quote_chars_cap",
                "char_length(quote) <= 400");

            // 25-word cap (ADR-051 T1). regexp_matches counts whitespace-separated tokens.
            t.HasCheckConstraint(
                "ck_mechanic_citations_quote_word_cap",
                "array_length(regexp_split_to_array(btrim(quote), '\\s+'), 1) <= 25");

            t.HasCheckConstraint(
                "ck_mechanic_citations_display_order_non_negative",
                "display_order >= 0");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").IsRequired();
        builder.Property(c => c.ClaimId).HasColumnName("claim_id").IsRequired();
        builder.Property(c => c.PdfPage).HasColumnName("pdf_page").IsRequired();

        builder.Property(c => c.Quote)
            .HasColumnName("quote")
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(c => c.ChunkId).HasColumnName("chunk_id");
        builder.Property(c => c.DisplayOrder).HasColumnName("display_order").IsRequired();

        builder.HasIndex(c => c.ClaimId).HasDatabaseName("ix_mechanic_citations_claim_id");
        builder.HasIndex(c => new { c.ClaimId, c.PdfPage })
            .HasDatabaseName("ix_mechanic_citations_claim_page");
        builder.HasIndex(c => c.ChunkId)
            .HasDatabaseName("ix_mechanic_citations_chunk_id")
            .HasFilter("chunk_id IS NOT NULL");

        // FK to text_chunks — nullable, ON DELETE SET NULL so chunk re-indexing doesn't drop citations.
        // Shadow-style relationship: the KB-owned TextChunk provides only the FK column here, not a navigation.
        builder.HasOne<Api.Infrastructure.Entities.TextChunkEntity>()
            .WithMany()
            .HasForeignKey(c => c.ChunkId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
