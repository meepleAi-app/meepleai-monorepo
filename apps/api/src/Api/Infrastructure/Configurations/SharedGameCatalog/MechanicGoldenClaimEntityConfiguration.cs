using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// EF configuration for <see cref="MechanicGoldenClaimEntity"/> (ADR-051 / M2 — AI comprehension certification).
/// </summary>
/// <remarks>
/// - Snake_case columns, soft-delete via <c>DeletedAt</c>, optimistic concurrency via <c>xmin</c>.
/// - Keywords persisted as <c>jsonb</c>; 768-dim sentence-transformer embedding as <c>vector(768)</c>.
/// - Global query filter hides soft-deleted rows.
/// </remarks>
internal sealed class MechanicGoldenClaimEntityConfiguration : IEntityTypeConfiguration<MechanicGoldenClaimEntity>
{
    public void Configure(EntityTypeBuilder<MechanicGoldenClaimEntity> builder)
    {
        builder.ToTable("mechanic_golden_claims", t =>
        {
            t.HasCheckConstraint(
                "ck_mechanic_golden_claims_section_range",
                "section BETWEEN 0 AND 5");

            t.HasCheckConstraint(
                "ck_mechanic_golden_claims_expected_page_positive",
                "expected_page > 0");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").IsRequired();
        builder.Property(c => c.SharedGameId).HasColumnName("shared_game_id").IsRequired();
        builder.Property(c => c.Section).HasColumnName("section").IsRequired();

        builder.Property(c => c.Statement)
            .HasColumnName("statement")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(c => c.ExpectedPage).HasColumnName("expected_page").IsRequired();

        builder.Property(c => c.SourceQuote)
            .HasColumnName("source_quote")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(c => c.KeywordsJson)
            .HasColumnName("keywords_json")
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(c => c.Embedding)
            .HasColumnName("embedding")
            .HasColumnType("vector(768)");

        builder.Property(c => c.CuratorUserId).HasColumnName("curator_user_id").IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // Soft delete
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.DeletedAt).HasColumnName("deleted_at");

        // Optimistic concurrency via PostgreSQL's system `xmin`.
        builder.Property(c => c.Xmin)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // === Indexes ===
        builder.HasIndex(c => c.SharedGameId).HasDatabaseName("ix_mechanic_golden_claims_shared_game_id");
        builder.HasIndex(c => new { c.SharedGameId, c.Section })
            .HasDatabaseName("ix_mechanic_golden_claims_shared_game_section");

        // === FK to SharedGame ===
        builder.HasOne(c => c.SharedGame)
            .WithMany()
            .HasForeignKey(c => c.SharedGameId)
            .OnDelete(DeleteBehavior.Restrict);

        // === Soft delete global query filter ===
        builder.HasQueryFilter(c => c.DeletedAt == null);
    }
}
