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

        // #3651 lotto 4 — allineata al pattern xmin di ADR-060. `IsRowVersion()` su un `byte[]`
        // NON produce il mapping alla colonna di sistema su Npgsql, malgrado il nome lo suggerisca:
        // serve la configurazione esplicita qui sotto. La colonna materializzata `row_version` è
        // rimossa dalla migration BggTosHashXminConcurrency.
        builder.Property(e => e.Xmin)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
