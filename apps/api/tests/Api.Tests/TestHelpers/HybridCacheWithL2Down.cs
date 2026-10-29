using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.TestHelpers;

/// <summary>
/// A real <see cref="HybridCache"/> whose second level (<see cref="IDistributedCache"/>, i.e. Redis in
/// production) is down: every call into it throws.
/// </summary>
/// <remarks>
/// Measured against the real <c>Microsoft.Extensions.Caching.Hybrid</c>, not assumed: with such an L2,
/// <c>RemoveAsync</c> and <c>RemoveByTagAsync</c> THROW (<see cref="InvalidOperationException"/> here),
/// while <c>SetAsync</c> and <c>GetOrCreateAsync</c> do not (they degrade to the local level). So the
/// invalidation after a committed write is the one step an outage turns into an error, and a mock that
/// throws a chosen exception would only test the exception we picked.
/// </remarks>
internal static class HybridCacheWithL2Down
{
    public static HybridCache Create()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDistributedCache>(new DownDistributedCache());
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private sealed class DownDistributedCache : IDistributedCache
    {
        private static InvalidOperationException Down() => new("redis is down");

        public byte[]? Get(string key) => throw Down();

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Down();

        public void Refresh(string key) => throw Down();

        public Task RefreshAsync(string key, CancellationToken token = default) => throw Down();

        public void Remove(string key) => throw Down();

        public Task RemoveAsync(string key, CancellationToken token = default) => throw Down();

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Down();

        public Task SetAsync(
            string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            => throw Down();
    }
}
