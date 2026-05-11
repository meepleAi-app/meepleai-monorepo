using Api.BoundedContexts.Administration.Domain.Services;
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

    public ProviderQuotaService(IProviderQuotaProviderFactory factory, IHybridCacheService cache)
    {
        _factory = factory;
        _cache = cache;
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

        var apiKey = Environment.GetEnvironmentVariable(provider.ApiKeyEnvVar) ?? string.Empty;
        if (string.IsNullOrEmpty(apiKey))
        {
            return new ProviderQuotaDto(
                ProviderName: providerName,
                QuotaSupported: true,
                TokenConfigured: false,
                UsedUsd: null,
                LimitUsd: null,
                RemainingUsd: null,
                ResetAt: null,
                ErrorCode: "not_configured",
                ErrorMessage: "API key environment variable not set",
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
}
