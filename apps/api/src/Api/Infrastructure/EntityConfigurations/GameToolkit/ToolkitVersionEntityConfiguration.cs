using Api.Infrastructure.Entities.GameToolkit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>
/// EF Core configuration for <see cref="ToolkitVersionEntity"/>
/// (issue #822 — Phase 5 schema foundation).
/// </summary>
internal class ToolkitVersionEntityConfiguration : IEntityTypeConfiguration<ToolkitVersionEntity>
{
    public void Configure(EntityTypeBuilder<ToolkitVersionEntity> builder)
    {
        builder.ToTable("ToolkitVersions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.ToolkitId).IsRequired();
        builder.Property(e => e.VersionNumber).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Changelog).HasMaxLength(4000);
        builder.Property(e => e.PublishedAt).IsRequired();
        builder.Property(e => e.PublishedBy).IsRequired();

        // Yank audit — all nullable: NULL when not yanked.
        builder.Property(e => e.YankedAt).IsRequired(false);
        builder.Property(e => e.YankReason).IsRequired(false).HasMaxLength(500);
        builder.Property(e => e.YankedBy).IsRequired(false);

        builder.Property(e => e.RowVersion).IsRowVersion();

        // FK to GameToolkitEntity. Cascade delete: yanking the parent toolkit
        // (admin hard-delete path, not the normal soft-delete via unpublish)
        // removes its version history with it.
        builder.HasOne(e => e.Toolkit)
            .WithMany()
            .HasForeignKey(e => e.ToolkitId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // Unique (ToolkitId, VersionNumber): enforces no version-number reuse
        // even after a yank (spec-panel 2026-05-18 §1).
        builder.HasIndex(e => new { e.ToolkitId, e.VersionNumber })
            .IsUnique()
            .HasDatabaseName("IX_ToolkitVersions_ToolkitId_VersionNumber");

        // Sort index for the versions list query (PublishedAt DESC per toolkit).
        builder.HasIndex(e => new { e.ToolkitId, e.PublishedAt })
            .HasDatabaseName("IX_ToolkitVersions_ToolkitId_PublishedAt");
    }
}
