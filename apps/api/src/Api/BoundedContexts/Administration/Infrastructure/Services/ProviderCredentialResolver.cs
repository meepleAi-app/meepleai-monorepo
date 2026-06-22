using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Middleware.Exceptions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Default <see cref="IProviderCredentialResolver"/> implementation: DB-active-row → env-var
/// cascade with a 5-minute in-process cache. Decrypts DB ciphertext via
/// <see cref="IDataProtectionProvider"/> with purpose <c>"ProviderCredentials"</c>.
///
/// Cache invalidation:
/// - Local: <see cref="Invalidate(string)"/> drops the entry from <see cref="IMemoryCache"/>.
/// - Cross-pod: <c>ProviderCacheInvalidationSubscriber</c> listens on Redis channel
///   <c>provider:cache-invalidate</c> and calls <see cref="Invalidate(string)"/> from a fresh
///   DI scope per message.
///
/// Throws <see cref="ProviderCredentialNotConfiguredException"/> (HTTP 503) when neither DB
/// nor env var is configured for the provider.
///
/// Issue #1859.
/// </summary>
internal sealed class ProviderCredentialResolver : IProviderCredentialResolver
{
    internal const string DataProtectionPurpose = "ProviderCredentials";
    internal static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IProviderCredentialRepository _repository;
    private readonly IDataProtectionProvider _protectionProvider;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ProviderCredentialResolver> _logger;

    public ProviderCredentialResolver(
        IProviderCredentialRepository repository,
        IDataProtectionProvider protectionProvider,
        IMemoryCache cache,
        ILogger<ProviderCredentialResolver> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _protectionProvider = protectionProvider ?? throw new ArgumentNullException(nameof(protectionProvider));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> ResolveAsync(string providerName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        var normalized = providerName.ToLowerInvariant();
        var cacheKey = CacheKey(normalized);

        if (_cache.TryGetValue(cacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
        {
            return cached;
        }

        var active = await _repository.GetActiveAsync(normalized, ct).ConfigureAwait(false);
        if (active is not null)
        {
            var protector = _protectionProvider.CreateProtector(DataProtectionPurpose);
            var plaintext = protector.Unprotect(active.EncryptedApiKey);
            _cache.Set(cacheKey, plaintext, CacheTtl);
            return plaintext;
        }

        var envVar = ProviderEnvVarMap.For(normalized);
        var envValue = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            _cache.Set(cacheKey, envValue, CacheTtl);
            return envValue;
        }

        _logger.LogWarning(
            "ProviderCredentialResolver: no DB row and no {EnvVar} env var configured for provider '{Provider}'",
            envVar, normalized);
        throw new ProviderCredentialNotConfiguredException(normalized);
    }

    public void Invalidate(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        var normalized = providerName.ToLowerInvariant();
        _cache.Remove(CacheKey(normalized));
        _logger.LogInformation(
            "ProviderCredentialResolver: cache invalidated for provider '{Provider}'",
            normalized);
    }

    private static string CacheKey(string normalized) => $"provider_cred:{normalized}";
}
