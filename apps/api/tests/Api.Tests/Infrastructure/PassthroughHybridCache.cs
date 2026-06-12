using Api.Services;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Test-only <see cref="IHybridCacheService"/> impl that always invokes the factory.
/// Use for integration tests that exercise a service performing read→write→read of the
/// same key in a single run — the default <c>Mock.Of&lt;IHybridCacheService&gt;()</c>
/// registered by <see cref="IntegrationServiceCollectionBuilder.CreateBase"/> returns
/// null without calling the factory, which masks any state change the write produced.
/// </summary>
/// <remarks>
/// Extracted from <c>SetRegistrationModeIntegrationTests</c> (issue #2162 follow-up).
/// Opt-in via <c>IntegrationServiceCollectionBuilder.CreateBase(connectionString, useHybridCachePassthrough: true)</c>.
/// </remarks>
internal sealed class PassthroughHybridCache : IHybridCacheService
{
    public Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<CancellationToken, Task<T>> factory,
        string[]? tags = null,
        TimeSpan? expiration = null,
        CancellationToken ct = default) where T : class
        => factory(ct);

    public Task RemoveAsync(string cacheKey, CancellationToken ct = default) => Task.CompletedTask;

    public Task<int> RemoveByTagAsync(string tag, CancellationToken ct = default) => Task.FromResult(0);

    public Task<int> RemoveByTagsAsync(string[] tags, CancellationToken ct = default) => Task.FromResult(0);

    public Task<HybridCacheStats> GetStatsAsync(CancellationToken ct = default) => Task.FromResult(new HybridCacheStats());
}
