using Microsoft.Extensions.Caching.Hybrid;
using StackExchange.Redis;

namespace Api.Services;

/// <summary>
/// Background service that subscribes to the Redis channel
/// <see cref="HybridCacheService.CrossReplicaInvalidationChannel"/> at startup.
/// Each message carries a single tag (string). On receipt the service evicts every
/// matching entry from its replica-local <see cref="HybridCache"/> L1 cache.
///
/// Cross-replica L1 invariant: when a writer on Replica A calls
/// <see cref="IHybridCacheService.RemoveByTagAcrossReplicasAsync"/>, the local
/// L1+L2 eviction is followed by a Redis Pub/Sub broadcast. Replicas B/C receive
/// the tag here and evict their local L1 entries. Without this subscriber the
/// non-publishing replicas would keep serving the L1 entry until its
/// <c>LocalCacheExpiration</c> ticked (15-30 min) — the bug ADR-062 closes.
///
/// Idempotent: the publishing replica also receives its own broadcast and re-runs
/// the eviction as a no-op (L1 entry already gone from Step 1 of the publish).
///
/// ADR-062 (KB-flag cache propagation strategy).
/// </summary>
internal sealed class HybridCacheInvalidationSubscriber : IHostedService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly HybridCache _hybridCache;
    private readonly ILogger<HybridCacheInvalidationSubscriber> _logger;
    private ChannelMessageQueue? _queue;

    public HybridCacheInvalidationSubscriber(
        IConnectionMultiplexer redis,
        HybridCache hybridCache,
        ILogger<HybridCacheInvalidationSubscriber> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _hybridCache = hybridCache ?? throw new ArgumentNullException(nameof(hybridCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var subscriber = _redis.GetSubscriber();
        _queue = await subscriber
            .SubscribeAsync(RedisChannel.Literal(HybridCacheService.CrossReplicaInvalidationChannel))
            .ConfigureAwait(false);

        _queue.OnMessage(async message =>
        {
            var tag = message.Message.ToString();
            if (string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            try
            {
                // RemoveByTagAsync on the Microsoft HybridCache evicts BOTH L1 (local replica)
                // and L2 (distributed). L2 is already empty when this fires (publisher cleared it)
                // — the L1 eviction is the work that matters here.
                await _hybridCache.RemoveByTagAsync(tag, CancellationToken.None).ConfigureAwait(false);

                _logger.LogInformation(
                    "Received cross-replica cache invalidation; evicted L1 entries for tag '{Tag}'",
                    tag);
            }
#pragma warning disable CA1031 // Pub/sub callback must not throw — would break the channel queue
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(
                    ex,
                    "Failed to evict cache entries for tag '{Tag}' after Redis cross-replica invalidation",
                    tag);
            }
        });

        _logger.LogInformation(
            "Subscribed to Redis channel '{Channel}' for cross-replica L1 cache invalidation",
            HybridCacheService.CrossReplicaInvalidationChannel);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_queue is not null)
        {
            await _queue.UnsubscribeAsync().ConfigureAwait(false);
            _queue = null;
            _logger.LogInformation(
                "Unsubscribed from Redis channel '{Channel}' for cross-replica L1 cache invalidation",
                HybridCacheService.CrossReplicaInvalidationChannel);
        }
    }
}
