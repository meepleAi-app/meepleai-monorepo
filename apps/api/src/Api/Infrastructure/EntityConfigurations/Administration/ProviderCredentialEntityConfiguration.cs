using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderCredentials;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.Administration;

/// <summary>
/// EF Core configuration for the <see cref="ProviderCredential"/> aggregate.
/// Issue #1859 — DB-backed provider key rotation with encrypted at-rest API keys.
/// </summary>
internal sealed class ProviderCredentialEntityConfiguration
    : IEntityTypeConfiguration<ProviderCredential>
{
    public void Configure(EntityTypeBuilder<ProviderCredential> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("provider_credentials");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.ProviderName)
            .HasColumnName("provider_name")
            .HasConversion(vo => vo.Value, raw => ProviderName.Create(raw))
            .IsRequired();

        builder.Property(e => e.EncryptedApiKey)
            .HasColumnName("encrypted_api_key")
            .IsRequired();

        builder.Property(e => e.Fingerprint)
            .HasColumnName("key_fingerprint")
            .HasConversion(vo => vo.Value, raw => KeyFingerprint.FromStorage(raw))
            .IsRequired();

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(e => e.RotatedAt)
            .HasColumnName("rotated_at")
            .IsRequired();

        builder.Property(e => e.RotatedByUserId)
            .HasColumnName("rotated_by_user_id")
            .IsRequired();

        builder.Property(e => e.PreviousCredentialId)
            .HasColumnName("previous_credential_id");

        // #3651: concorrenza ottimistica via `xmin`, la colonna di sistema di PostgreSQL.
        //
        // Qui c'era `.Property(e => e.RowVersion).HasColumnName("row_version").IsRowVersion()` su
        // una `bytea`, con un commento che riconosceva il difetto invece di correggerlo: «this
        // table has no trigger/xmin populating the token, so it stays NULL and provides no real
        // optimistic-concurrency detection». Restava quindi una dichiarazione senza effetto — EF
        // confrontava NULL = NULL a ogni update e nessun conflitto veniva rilevato.
        //
        // Anche il vincolo storico che imponeva la colonna nullable sparisce: serviva perché
        // .IsRowVersion() faceva omettere row_version dall'INSERT, e una `bytea` NOT NULL alzava
        // un 23502 alla prima credenziale. `xmin` è di sistema: Postgres la valorizza da sé e non
        // compare mai in un INSERT.
        //
        // Stesso pattern di LiveGameSessionEntityConfiguration:148-152 e GameNightPlaylist.
        builder.Property(e => e.Xmin)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Partial unique index: 1 sola row attiva per provider (Postgres filtered index)
        builder.HasIndex(e => e.ProviderName)
            .IsUnique()
            .HasFilter("is_active = true")
            .HasDatabaseName("ux_provider_credentials_active_one");

        builder.HasIndex(e => new { e.ProviderName, e.RotatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_provider_credentials_rotated_at");

        // Domain events are not persisted (in-memory only)
        builder.Ignore(e => e.DomainEvents);
    }
}
