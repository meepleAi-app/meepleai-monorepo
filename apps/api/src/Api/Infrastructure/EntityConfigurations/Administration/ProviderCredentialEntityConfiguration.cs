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

        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion();

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
