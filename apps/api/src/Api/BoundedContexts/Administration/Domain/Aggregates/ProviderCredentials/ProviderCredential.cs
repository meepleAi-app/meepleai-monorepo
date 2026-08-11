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
    // #3651: token di concorrenza ottimistica sulla colonna di sistema `xmin`, come le entità
    // migrate da #2305.
    //
    // Prima era `byte[]? RowVersion` su una colonna `bytea`, e il commento qui dichiarava che il
    // token «non è popolato su questa tabella» accettandolo come normale: la protezione era
    // dichiarata dalla configurazione EF ma non esisteva, perché Postgres non valorizza una
    // `bytea` da solo e il trigger che lo faceva è stato rimosso da #2305. Restava NULL su ogni
    // riga, EF confrontava NULL = NULL e ogni update passava.
    //
    // L'indice parziale ux_provider_credentials_active_one continua a garantire l'invariante
    // «una sola riga attiva per provider», ma copre l'INSERT della nuova credenziale, non
    // l'UPDATE che disattiva la precedente: due rotazioni concorrenti disattivavano entrambe la
    // stessa riga senza che nulla lo segnalasse.
    //
    // `uint` e non `byte[]`: xmin è di tipo `xid`. La proprietà non è esposta da alcun DTO.
    //
    // Get-only come lo era RowVersion: il dominio non deve mai assegnarla, la scrive solo EF Core
    // sul backing field. Un `private set` qui verrebbe segnalato da S1144 come setter inutilizzato,
    // perché l'unico scrittore è la reflection.
    public uint Xmin { get; }

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
