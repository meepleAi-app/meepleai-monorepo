using Api.Infrastructure.Entities.UserLibrary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.UserLibrary;

/// <summary>
/// Entity Framework configuration for LibraryShareLink entity.
/// </summary>
internal sealed class LibraryShareLinkEntityConfiguration : IEntityTypeConfiguration<LibraryShareLinkEntity>
{
    public void Configure(EntityTypeBuilder<LibraryShareLinkEntity> builder)
    {
        builder.ToTable("library_share_links");

        builder.HasKey(sl => sl.Id);

        builder.Property(sl => sl.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(sl => sl.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(sl => sl.ShareToken)
            .HasColumnName("share_token")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(sl => sl.PrivacyLevel)
            .HasColumnName("privacy_level")
            .IsRequired();

        builder.Property(sl => sl.IncludeNotes)
            .HasColumnName("include_notes")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(sl => sl.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(sl => sl.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(sl => sl.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(sl => sl.ViewCount)
            .HasColumnName("view_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(sl => sl.LastAccessedAt)
            .HasColumnName("last_accessed_at");

        // Indexes for query performance
        builder.HasIndex(sl => sl.ShareToken)
            .IsUnique()
            .HasDatabaseName("ix_library_share_links_share_token");

        builder.HasIndex(sl => sl.UserId)
            .HasDatabaseName("ix_library_share_links_user_id");

        builder.HasIndex(sl => sl.ExpiresAt)
            .HasDatabaseName("ix_library_share_links_expires_at");

        builder.HasIndex(sl => sl.RevokedAt)
            .HasDatabaseName("ix_library_share_links_revoked_at");

        builder.HasIndex(sl => sl.PrivacyLevel)
            .HasDatabaseName("ix_library_share_links_privacy_level");

        // Foreign key to User
        builder.HasOne(sl => sl.User)
            .WithMany()
            .HasForeignKey(sl => sl.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
