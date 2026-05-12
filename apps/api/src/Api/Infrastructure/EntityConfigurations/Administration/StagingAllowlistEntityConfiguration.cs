using Api.Infrastructure.Entities.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.Administration;

/// <summary>
/// EF configuration for the staging_allowlist table (#845).
/// Soft-delete via partial unique index on email (only active rows enforce uniqueness).
/// </summary>
internal class StagingAllowlistEntityConfiguration : IEntityTypeConfiguration<StagingAllowlistEntity>
{
    public void Configure(EntityTypeBuilder<StagingAllowlistEntity> builder)
    {
        builder.ToTable("staging_allowlist");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.Email)
            .HasColumnName("email")
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(e => e.AddedByUserId)
            .HasColumnName("added_by_user_id");

        builder.Property(e => e.AddedAt)
            .HasColumnName("added_at")
            .IsRequired();

        builder.Property(e => e.Note)
            .HasColumnName("note")
            .HasMaxLength(500);

        builder.Property(e => e.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(e => e.DeletedByUserId)
            .HasColumnName("deleted_by_user_id");

        // Partial unique index: only enforce email uniqueness on active rows.
        // Allows re-adding a previously soft-deleted email without removing audit history.
        builder.HasIndex(e => e.Email)
            .IsUnique()
            .HasDatabaseName("ix_staging_allowlist_email_active")
            .HasFilter("is_deleted = false");

        builder.HasIndex(e => e.IsDeleted)
            .HasDatabaseName("ix_staging_allowlist_is_deleted");

        // Soft-delete query filter per CLAUDE.md "Key Data Patterns":
        // IsDeleted + DeletedAt + HasQueryFilter(e => !e.IsDeleted).
        // Repository methods that need to read soft-deleted rows (e.g. UpdateAsync
        // soft-delete path) must use .IgnoreQueryFilters() to bypass this filter.
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
