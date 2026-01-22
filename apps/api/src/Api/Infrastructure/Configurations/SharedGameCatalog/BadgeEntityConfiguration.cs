using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// Entity Framework configuration for Badge entity.
/// ISSUE-2731: Infrastructure - EF Core Migrations e Repository
/// </summary>
internal sealed class BadgeEntityConfiguration : IEntityTypeConfiguration<BadgeEntity>
{
    public void Configure(EntityTypeBuilder<BadgeEntity> builder)
    {
        builder.ToTable("badges");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.Code)
            .HasColumnName("code")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.IconUrl)
            .HasColumnName("icon_url")
            .HasMaxLength(500);

        builder.Property(e => e.Tier)
            .HasColumnName("tier")
            .IsRequired();

        builder.Property(e => e.Category)
            .HasColumnName("category")
            .IsRequired();

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(e => e.DisplayOrder)
            .HasColumnName("display_order")
            .IsRequired();

        builder.Property(e => e.RequirementJson)
            .HasColumnName("requirement")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.ModifiedAt)
            .HasColumnName("modified_at");

        // Indexes
        builder.HasIndex(e => e.Code)
            .IsUnique()
            .HasDatabaseName("ix_badges_code_unique");

        builder.HasIndex(e => e.Tier)
            .HasDatabaseName("ix_badges_tier");

        builder.HasIndex(e => e.Category)
            .HasDatabaseName("ix_badges_category");

        builder.HasIndex(e => e.IsActive)
            .HasDatabaseName("ix_badges_is_active");

        builder.HasIndex(e => e.DisplayOrder)
            .HasDatabaseName("ix_badges_display_order");
    }
}
