using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderCredentials;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

/// <summary>
/// Repository for <see cref="ProviderCredential"/> aggregate.
/// Issue #1859.
/// </summary>
public interface IProviderCredentialRepository
{
    /// <summary>
    /// Returns the currently active credential for the provider (IsActive=true), or null
    /// when no DB-backed credential exists for the provider (consumers fall back to env-var).
    /// </summary>
    Task<ProviderCredential?> GetActiveAsync(string providerName, CancellationToken ct);

    /// <summary>
    /// Returns the most recent credential (active or not) for the provider, by RotatedAt DESC.
    /// Used by the rate-limit guard in <c>RotateProviderKeyCommandHandler</c> to enforce
    /// the 24h-per-provider cooldown.
    /// </summary>
    Task<ProviderCredential?> GetLastRotationAsync(string providerName, CancellationToken ct);

    Task AddAsync(ProviderCredential credential, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
