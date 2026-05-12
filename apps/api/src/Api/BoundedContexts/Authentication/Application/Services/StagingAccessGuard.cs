using Api.BoundedContexts.Administration.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.Authentication.Application.Services;

/// <summary>
/// DB-backed implementation of <see cref="IStagingAccessGuard"/> (#845).
/// Reads from <c>staging_allowlist</c> via <see cref="IStagingAllowlistRepository"/>
/// and caches the resulting set in <see cref="IMemoryCache"/> for 60 seconds.
/// </summary>
/// <remarks>
/// Registered as <b>Singleton</b>, but uses <see cref="IServiceScopeFactory"/> to resolve
/// the scoped repository on cache miss. Cache invalidation on add/remove flows via
/// the domain event handler <c>StagingAllowlistCacheInvalidator</c>.
/// </remarks>
internal sealed class StagingAccessGuard : IStagingAccessGuard
{
    internal const string CacheKey = "staging_allowlist:emails";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public StagingAccessGuard(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public async ValueTask<bool> IsEmailAllowedAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var allowed = await GetAllowedEmailsAsync(cancellationToken).ConfigureAwait(false);

        // FAIL-CLOSED (#845): empty allowlist denies all in Staging.
        // The middleware only registers in Staging env, so prod/dev are unaffected.
        if (allowed.Count == 0)
        {
            return false;
        }

        return allowed.Contains(email.Trim().ToLowerInvariant());
    }

    public async ValueTask<bool> HasNonEmptyAllowlistAsync(CancellationToken cancellationToken = default)
    {
        var allowed = await GetAllowedEmailsAsync(cancellationToken).ConfigureAwait(false);
        return allowed.Count > 0;
    }

    public void InvalidateCache()
    {
        _cache.Remove(CacheKey);
    }

    private async Task<IReadOnlySet<string>> GetAllowedEmailsAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue<IReadOnlySet<string>>(CacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IStagingAllowlistRepository>();
        var emails = await repository.GetAllowedEmailsAsync(cancellationToken).ConfigureAwait(false);

        _cache.Set(CacheKey, emails, CacheTtl);
        return emails;
    }
}
