using Api.BoundedContexts.Administration.Domain.Services;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Services;
using Api.Services.Providers.Quota;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Orchestrates provider quota fetch with HybridCache (5min TTL).
/// Issue #936 (G2). Returns 501 semantics (QuotaSupported:false) for unknown providers.
/// </summary>
internal sealed class ProviderQuotaService : IProviderQuotaService
{
    private const int CacheTtlSeconds = 300; // 5 minutes
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(CacheTtlSeconds);

    private readonly IProviderQuotaProviderFactory _factory;
    private readonly IHybridCacheService _cache;
    private readonly IProviderCredentialResolver _credentialResolver;

    public ProviderQuotaService(
        IProviderQuotaProviderFactory factory,
        IHybridCacheService cache,
        IProviderCredentialResolver credentialResolver)
    {
        _factory = factory;
        _cache = cache;
        _credentialResolver = credentialResolver;
    }

    public async Task<ProviderQuotaDto> GetQuotaAsync(string providerName, CancellationToken cancellationToken)
    {
        var provider = _factory.GetProvider(providerName);
        if (provider is null)
        {
            // Quota is not supported for this provider (or unknown).
            return new ProviderQuotaDto(
                ProviderName: providerName,
                QuotaSupported: false,
                TokenConfigured: false,
                UsedUsd: null,
                LimitUsd: null,
                RemainingUsd: null,
                ResetAt: null,
                ErrorCode: "quota_not_supported",
                ErrorMessage: "Provider does not expose a public quota API",
                FetchedAt: DateTime.UtcNow,
                CacheTtlSeconds: 0);
        }

        // Issue #3044: resolve the key via IProviderCredentialResolver (DB active-row → env-var
        // fallback) instead of reading the env var directly, so a rotated key (#1859) is honoured.
        // Resolved BEFORE the HybridCache (as the env-read was) — the resolver has its own 5min cache.
        string apiKey;
        try
        {
            apiKey = await _credentialResolver.ResolveAsync(providerName, cancellationToken).ConfigureAwait(false);
        }
        catch (ProviderCredentialNotConfiguredException)
        {
            // Neither a DB credential nor an env var configured → graceful not_configured (NOT 503).
            return new ProviderQuotaDto(
                ProviderName: providerName,
                QuotaSupported: true,
                TokenConfigured: false,
                UsedUsd: null,
                LimitUsd: null,
                RemainingUsd: null,
                ResetAt: null,
                ErrorCode: "not_configured",
                ErrorMessage: "No active credential or env-var configured for provider",
                FetchedAt: DateTime.UtcNow,
                CacheTtlSeconds: 0);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Any other resolver failure (DataProtection decrypt / rotated key-ring →
            // CryptographicException, or an unmapped provider → ArgumentException) must NOT
            // surface as a 500 on GET /admin/providers/{name}/quota. Degrade gracefully.
            return new ProviderQuotaDto(
                ProviderName: providerName,
                QuotaSupported: true,
                TokenConfigured: false,
                UsedUsd: null,
                LimitUsd: null,
                RemainingUsd: null,
                ResetAt: null,
                ErrorCode: "credential_error",
                ErrorMessage: "Failed to resolve provider credential",
                FetchedAt: DateTime.UtcNow,
                CacheTtlSeconds: 0);
        }

        var cacheKey = $"provider-quota:{providerName.ToLowerInvariant()}";
        return await _cache.GetOrCreateAsync(
            cacheKey,
            async ct =>
            {
                var result = await provider.FetchAsync(apiKey, ct).ConfigureAwait(false);
                return new ProviderQuotaDto(
                    ProviderName: providerName,
                    QuotaSupported: true,
                    TokenConfigured: true,
                    UsedUsd: result.UsedUsd,
                    LimitUsd: result.LimitUsd,
                    RemainingUsd: result.RemainingUsd,
                    ResetAt: result.ResetAt,
                    ErrorCode: result.ErrorCode,
                    ErrorMessage: result.ErrorMessage,
                    FetchedAt: DateTime.UtcNow,
                    CacheTtlSeconds: CacheTtlSeconds);
            },
            tags: new[] { "provider-quota", $"provider:{providerName.ToLowerInvariant()}" },
            expiration: CacheTtl,
            ct: cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProviderQuotaDto>> GetAllQuotasAsync(CancellationToken cancellationToken)
    {
        // Sequential (NOT Task.WhenAll): since #3044 GetQuotaAsync resolves the key via the scoped
        // IProviderCredentialResolver → scoped MeepleAiDbContext. A parallel fan-out would issue
        // concurrent EF queries on the SAME scoped DbContext (unsupported → InvalidOperationException)
        // on a cold resolver cache. N is tiny and the resolver/quota 5min caches make this a warm
        // in-memory read almost always. Per-provider exception isolation preserved (GetQuotaSafeAsync).
        var results = new List<ProviderQuotaDto>();
        foreach (var name in _factory.SupportedProviderNames)
        {
            results.Add(await GetQuotaSafeAsync(name, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    private async Task<ProviderQuotaDto> GetQuotaSafeAsync(string providerName, CancellationToken cancellationToken)
    {
        try
        {
            return await GetQuotaAsync(providerName, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Degrade this single provider to an error DTO; the aggregate still returns the rest.
            return new ProviderQuotaDto(
                ProviderName: providerName,
                QuotaSupported: true,
                TokenConfigured: true,
                UsedUsd: null,
                LimitUsd: null,
                RemainingUsd: null,
                ResetAt: null,
                ErrorCode: "fetch_error",
                ErrorMessage: ex.Message,
                FetchedAt: DateTime.UtcNow,
                CacheTtlSeconds: 0);
        }
    }
}
