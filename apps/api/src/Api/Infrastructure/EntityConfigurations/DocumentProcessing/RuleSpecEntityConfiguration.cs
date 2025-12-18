using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

internal class RuleSpecEntityConfiguration : IEntityTypeConfiguration<RuleSpecEntity>
{
    public void Configure(EntityTypeBuilder<RuleSpecEntity> builder)
    {
        builder.ToTable("rule_specs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.Version).IsRequired().HasMaxLength(32);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.GameId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.CreatedByUserId).HasMaxLength(64);
        // EDIT-06: Version timeline support
        builder.Property(e => e.ParentVersionId).HasMaxLength(64);
        builder.Property(e => e.MergedFromVersionIds).HasMaxLength(1024);
        builder.HasOne(e => e.Game)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
        // EDIT-06: Parent version relationship (self-referencing)
        builder.HasOne(e => e.ParentVersion)
            .WithMany()
            .HasForeignKey(e => e.ParentVersionId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(e => new { e.GameId, e.Version }).IsUnique();
        builder.HasIndex(e => e.ParentVersionId);

        // Issue #2055: Optimistic concurrency control for collaborative editing
        builder.Property(e => e.RowVersion)
            .IsRowVersion();
    }
}
