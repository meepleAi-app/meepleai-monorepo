using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// EF configuration for <see cref="MechanicGoldenBggTagEntity"/> (ADR-051 / M2 — BGG mechanic tag snapshot).
/// </summary>
/// <remarks>
/// Append/replace semantics: no <c>xmin</c> concurrency token and no soft-delete. Unique index on
/// (shared_game_id, name) enforces dedupe during BGG sync upserts.
/// </remarks>
internal sealed class MechanicGoldenBggTagEntityConfiguration : IEntityTypeConfiguration<MechanicGoldenBggTagEntity>
{
    public void Configure(EntityTypeBuilder<MechanicGoldenBggTagEntity> builder)
    {
        builder.ToTable("mechanic_golden_bgg_tags");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id").IsRequired();
        builder.Property(t => t.SharedGameId).HasColumnName("shared_game_id").IsRequired();

        builder.Property(t => t.Name)
            .HasColumnName("name")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(t => t.Category)
            .HasColumnName("category")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(t => t.ImportedAt).HasColumnName("imported_at").IsRequired();

        // === Indexes ===
        builder.HasIndex(t => t.SharedGameId).HasDatabaseName("ix_mechanic_golden_bgg_tags_shared_game_id");

        // Dedupe: at most one tag with a given (game, name) — supports sync upsert semantics.
        builder.HasIndex(t => new { t.SharedGameId, t.Name })
            .HasDatabaseName("ux_mechanic_golden_bgg_tags_shared_game_name")
            .IsUnique();

        // === FK to SharedGame ===
        builder.HasOne(t => t.SharedGame)
            .WithMany()
            .HasForeignKey(t => t.SharedGameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
