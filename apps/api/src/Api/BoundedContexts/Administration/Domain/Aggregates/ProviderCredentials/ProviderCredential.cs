using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderCredentials.Events;
using Api.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.Administration.Domain.Aggregates.ProviderCredentials;

/// <summary>
/// Aggregate root tracking a provider's API key (encrypted via <c>IDataProtector</c>).
/// Issue #1859. Decorated with <see cref="AuditableAttribute"/> so
/// <c>AuditingSaveChangesInterceptor</c> captures before/after snapshots automatically
/// (see design §11.2 — automatic audit Details JSON, no manual handler code).
/// </summary>
[Auditable]
public sealed class ProviderCredential
{
    private readonly List<INotification> _domainEvents = new();

    public Guid Id { get; private set; }
    public ProviderName ProviderName { get; private set; } = default!;
    public string EncryptedApiKey { get; private set; } = default!;
    public KeyFingerprint Fingerprint { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public DateTime RotatedAt { get; private set; }
    public Guid RotatedByUserId { get; private set; }
    public Guid? PreviousCredentialId { get; private set; }
    // Nullable so the Npgsql provider can omit the store-generated row_version from the
    // INSERT (a NOT NULL bytea column raised a 23502 violation on the first insert).
    // Get-only: EF Core populates it via reflection; optimistic concurrency applies on UPDATE.
    public byte[]? RowVersion { get; }

    public IReadOnlyCollection<INotification> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private ProviderCredential() { }   // EF Core

    public static ProviderCredential Create(
        ProviderName provider,
        string encryptedKey,
        KeyFingerprint fingerprint,
        Guid rotatedByUserId,
        Guid? previousCredentialId,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrEmpty(encryptedKey);
        ArgumentNullException.ThrowIfNull(fingerprint);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (rotatedByUserId == Guid.Empty)
            throw new ArgumentException("RotatedByUserId required", nameof(rotatedByUserId));

        var credential = new ProviderCredential
        {
            Id = Guid.NewGuid(),
            ProviderName = provider,
            EncryptedApiKey = encryptedKey,
            Fingerprint = fingerprint,
            IsActive = true,
            RotatedAt = timeProvider.GetUtcNow().UtcDateTime,
            RotatedByUserId = rotatedByUserId,
            PreviousCredentialId = previousCredentialId
        };

        credential._domainEvents.Add(new ProviderKeyRotatedEvent(
            CredentialId: credential.Id,
            ProviderName: provider.Value,
            NewFingerprint: fingerprint.Value,
            PreviousFingerprint: null,
            RotatedByUserId: rotatedByUserId,
            RotatedAt: credential.RotatedAt));

        return credential;
    }

    /// <summary>
    /// Marks the credential as no longer active. Idempotent.
    /// Called on the previous active row when a new rotation is being applied.
    /// </summary>
    public void Deactivate(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        IsActive = false;
    }
}
