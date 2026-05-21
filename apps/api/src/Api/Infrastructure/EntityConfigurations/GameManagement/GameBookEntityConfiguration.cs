using Api.BoundedContexts.GameManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameManagement;

internal class GameBookEntityConfiguration : IEntityTypeConfiguration<GameBook>
{
    public void Configure(EntityTypeBuilder<GameBook> builder)
    {
        builder.ToTable("game_books");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.OwnsOne(e => e.GameRef, gr =>
        {
            gr.Property(p => p.Id).HasColumnName("game_ref_id").IsRequired();
            gr.Property(p => p.Kind).HasColumnName("game_ref_kind")
              .HasConversion<short>().IsRequired();
        });

        builder.Property(e => e.OwnerUserId).HasColumnName("owner_user_id");
        builder.Property(e => e.DisplayName).HasColumnName("display_name")
               .HasMaxLength(120).IsRequired();
        builder.Property(e => e.Roles).HasColumnName("roles")
               .HasConversion<int>().IsRequired();
        builder.Property(e => e.ParagraphScheme).HasColumnName("paragraph_scheme")
               .HasConversion<short>().IsRequired();
        builder.Property(e => e.Language).HasColumnName("language")
               .HasMaxLength(2).IsRequired();
        builder.Property(e => e.SequentialRead).HasColumnName("sequential_read")
               .HasDefaultValue(false).IsRequired();
        builder.Property(e => e.KbSourceDocId).HasColumnName("kb_source_doc_id");
        builder.Property(e => e.PhysicalOnly).HasColumnName("physical_only")
               .HasDefaultValue(false).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(e => e.DeletedAt).HasColumnName("deleted_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted")
               .HasDefaultValue(false).IsRequired();
        builder.Property(e => e.RowVersion).HasColumnName("row_version")
               .IsRowVersion();

        // A0.2 (#1320) lesson: composite indexes spanning owned-type columns (game_ref_kind,
        // game_ref_id) AND parent columns (deleted_at) cannot be expressed via HasIndex(...) in
        // EF Core 9 — owned-type properties are not addressable from the parent EntityTypeBuilder.
        // The composite index `ix_game_books_game_ref` over (game_ref_kind, game_ref_id, deleted_at)
        // is declared via raw SQL in the A6 migration (CreateGameBooks). See the matching pattern
        // in GamebookCampaignSessionEntityConfiguration.cs (A0.2).

        builder.HasIndex(e => new { e.OwnerUserId, e.DeletedAt })
               .HasDatabaseName("ix_game_books_owner_user_id")
               .HasFilter("owner_user_id IS NOT NULL");

        // Unique kb_source for community books only
        builder.HasIndex(e => e.KbSourceDocId)
               .IsUnique()
               .HasDatabaseName("ux_game_books_kb_source_community")
               .HasFilter("kb_source_doc_id IS NOT NULL AND owner_user_id IS NULL AND deleted_at IS NULL");

        // Soft-delete query filter
        builder.HasQueryFilter(e => !e.IsDeleted);

        // CHECK constraint: physical_only=true ⇒ kb_source_doc_id IS NULL
        builder.ToTable(t => t.HasCheckConstraint(
            "chk_game_books_physical_kb_coherence",
            "(physical_only = true AND kb_source_doc_id IS NULL) OR (physical_only = false)"));
    }
}
