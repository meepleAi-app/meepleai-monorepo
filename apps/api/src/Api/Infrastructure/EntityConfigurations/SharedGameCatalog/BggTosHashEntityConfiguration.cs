using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SharedGameCatalog;

/// <summary>
/// EF configuration for <see cref="BggTosHashEntity"/>. Singleton table
/// <c>bgg_tos_hashes</c> — one row keyed by <see cref="BggTosHashEntity.SingletonId"/>.
/// Issue #1903 M7.1.
/// </summary>
internal sealed class BggTosHashEntityConfiguration : IEntityTypeConfiguration<BggTosHashEntity>
{
    public void Configure(EntityTypeBuilder<BggTosHashEntity> builder)
    {
        builder.ToTable("bgg_tos_hashes");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.CurrentHash)
            .HasColumnName("current_hash")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.LastCheckedAt)
            .HasColumnName("last_checked_at")
            .IsRequired();

        builder.Property(e => e.LastChangedAt)
            .HasColumnName("last_changed_at");

        builder.Property(e => e.ChangeCount)
            .HasColumnName("change_count")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion();
    }
}
