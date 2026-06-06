namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Resolves the active API key for a provider, cascading from DB-backed credential row
/// (via <see cref="Api.BoundedContexts.Administration.Domain.Repositories.IProviderCredentialRepository"/>)
/// to env-var fallback (see <see cref="ProviderEnvVarMap"/>). The resolved plaintext is cached
/// in-process for a short TTL (5 minutes) and invalidated either locally
/// (<see cref="Invalidate(string)"/>) or cross-pod via Redis pub/sub
/// (<c>ProviderCacheInvalidationSubscriber</c>).
///
/// Implementations throw
/// <see cref="Api.Middleware.Exceptions.ProviderCredentialNotConfiguredException"/> (HTTP 503)
/// when neither a DB row nor an env var is configured for the requested provider.
///
/// Issue #1859.
/// </summary>
public interface IProviderCredentialResolver
{
    /// <summary>
    /// Returns the plaintext API key for <paramref name="providerName"/>.
    /// Cascades DB → env-var. Throws when neither is configured.
    /// </summary>
    Task<string> ResolveAsync(string providerName, CancellationToken ct);

    /// <summary>
    /// Removes the cached entry for <paramref name="providerName"/>. The next
    /// <see cref="ResolveAsync(string, CancellationToken)"/> call will re-read from the repository
    /// (and env var on fallback). Called by the Redis subscriber after a remote rotation, and by
    /// the local pod for tests.
    /// </summary>
    void Invalidate(string providerName);
}
