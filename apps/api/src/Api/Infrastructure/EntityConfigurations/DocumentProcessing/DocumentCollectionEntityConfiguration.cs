using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>
/// EF Core configuration for DocumentCollectionEntity.
/// Issue #2051: Multi-document collection persistence
/// </summary>
public class DocumentCollectionEntityConfiguration : IEntityTypeConfiguration<DocumentCollectionEntity>
{
    public void Configure(EntityTypeBuilder<DocumentCollectionEntity> builder)
    {
        builder.ToTable("document_collections");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.GameId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.Property(e => e.DocumentsJson).IsRequired().HasDefaultValue("[]");

        // Foreign keys
        builder.HasOne(e => e.Game)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(e => e.GameId);
        builder.HasIndex(e => new { e.CreatedByUserId, e.CreatedAt });
    }
}
