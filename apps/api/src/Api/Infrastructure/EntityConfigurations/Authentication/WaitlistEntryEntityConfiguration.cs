using Api.Infrastructure.Entities.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>
/// EF Core configuration for <see cref="WaitlistEntryEntity"/>.
/// Spec §3.5 (2026-04-27-v2-migration-wave-a-2-join.md).
/// </summary>
internal class WaitlistEntryEntityConfiguration : IEntityTypeConfiguration<WaitlistEntryEntity>
{
    public void Configure(EntityTypeBuilder<WaitlistEntryEntity> builder)
    {
        builder.ToTable("waitlist_entries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Email).IsRequired().HasMaxLength(254);
        builder.Property(e => e.Name).HasMaxLength(80);
        builder.Property(e => e.GamePreferenceId).IsRequired().HasMaxLength(40);
        builder.Property(e => e.GamePreferenceOther).HasMaxLength(80);
        builder.Property(e => e.NewsletterOptIn).IsRequired();
        builder.Property(e => e.Position).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.ContactedAt);

        // Unique index on Email — handler normalizes to lowercase before insert,
        // so a plain unique index is sufficient for case-insensitive duplicate detection.
        builder.HasIndex(e => e.Email).IsUnique();

        // Index on Position for GetMaxPositionAsync() performance
        builder.HasIndex(e => e.Position);

        // Index on CreatedAt for chronological admin queries
        builder.HasIndex(e => e.CreatedAt);
    }
}
